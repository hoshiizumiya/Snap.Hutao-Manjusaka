// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.Core;
using Snap.Hutao.Core.Logging;
using Snap.Hutao.Core.Shell;
using Snap.Hutao.Service.Navigation;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Service.Update;
using System.ComponentModel;
using Windows.Foundation;
using Snap.Hutao.Service;
using Microsoft.Extensions.DependencyInjection;

namespace Snap.Hutao.ViewModel.Setting;

[Service(ServiceLifetime.Scoped)]
internal sealed partial class SettingViewModel : Abstraction.ViewModel, INavigationRecipient
{
    public const string UIGFImportExport = nameof(UIGFImportExport);

    private readonly IShellLinkInterop shellLinkInterop;
    private readonly IUpdateService updateService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;
    private readonly AutoStartService autoStartService;

    private readonly WeakReference<ScrollViewer> weakScrollViewer = new(default!);
    private readonly WeakReference<Border> weakGachaLogBorder = new(default!);

    private AppOptions? appOptions;

    [GeneratedConstructor]
    public partial SettingViewModel(IServiceProvider serviceProvider);

    public partial SettingGeetestViewModel Geetest { get; }

    public partial SettingAppearanceViewModel Appearance { get; }

    public partial SettingStorageViewModel Storage { get; }

    public partial SettingHotKeyViewModel HotKey { get; }

    public partial SettingHomeViewModel Home { get; }

    public partial SettingGameViewModel Game { get; }

    public partial SettingGachaLogViewModel GachaLog { get; }

    public partial SettingWebViewViewModel WebView { get; }

    [ObservableProperty]
    public partial string? UpdateInfo { get; set; }

    public bool RunElevated
    {
        get => appOptions?.RunElevated?.Value ?? false;
        set
        {
            if (appOptions is null)
            {
                return;
            }

            if (appOptions.RunElevated.Value == value)
            {
                return;
            }

            appOptions.RunElevated.Value = value;
            OnPropertyChanged(nameof(RunElevated));
        }
    }

    public bool IsStartupEnabled
    {
        get => appOptions?.IsStartupEnabled?.Value ?? false;
        set
        {
            if (appOptions is null)
            {
                return;
            }

            if (appOptions.IsStartupEnabled.Value == value)
            {
                return;
            }

            appOptions.IsStartupEnabled.Value = value;
            OnPropertyChanged(nameof(IsStartupEnabled));
        }
    }

    public void AttachXamlElement(ScrollViewer scrollViewer, Border gachaLogBorder)
    {
        weakScrollViewer.SetTarget(scrollViewer);
        weakGachaLogBorder.SetTarget(gachaLogBorder);
    }

    public async ValueTask<bool> ReceiveAsync(INavigationExtraData data, CancellationToken token)
    {
        if (!await Initialization.Task.ConfigureAwait(false))
        {
            return false;
        }

        if (!weakScrollViewer.TryGetTarget(out ScrollViewer? scrollViewer) ||
            !weakGachaLogBorder.TryGetTarget(out Border? gachaLogBorder))
        {
            return false;
        }

        if (data.Data is UIGFImportExport)
        {
            await taskContext.SwitchToMainThreadAsync();
            Point point = gachaLogBorder.TransformToVisual(scrollViewer).TransformPoint(new(0, 0));
            scrollViewer.ChangeView(null, point.Y, null, true);
            return true;
        }

        return false;
    }

    protected override ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        MakeSubViewModel([Geetest, Appearance, Storage, HotKey, Home, Game, GachaLog, WebView]);

        Storage.CacheFolderView = new(taskContext, HutaoRuntime.LocalCacheDirectory);
        Storage.DataFolderView = new(taskContext, HutaoRuntime.DataDirectory);

        UpdateInfo = updateService.UpdateInfo;

        try
        {
            bool startup = autoStartService.IsStartupEnabled();
            bool runElevated = autoStartService.IsRunElevatedEnabled();
            AppOptions options = Ioc.Default.GetRequiredService<AppOptions>();
            options.IsStartupEnabled.Value = startup;
            options.RunElevated.Value = runElevated;

            appOptions = options;
            
            // Keep RunElevated property in sync when AppOptions.RunElevated changes
            options.RunElevated.PropertyChanged += (s, e) =>
            {
                try
                {
                    taskContext.InvokeOnMainThread(() => OnPropertyChanged(nameof(RunElevated)));
                }
                catch
                {
                    // ignore is a must
                }
            };
            
            // Keep IsStartupEnabled property in sync when AppOptions.IsStartupEnabled changes
            options.IsStartupEnabled.PropertyChanged += (s, e) =>
            {
                try
                {
                    taskContext.InvokeOnMainThread(() => OnPropertyChanged(nameof(IsStartupEnabled)));
                }
                catch
                {
                    // ignore
                }
            };
            
            // Manually trigger PropertyChanged to update UI with initial values
            OnPropertyChanged(nameof(RunElevated));
            OnPropertyChanged(nameof(IsStartupEnabled));
        }
        catch
        {
            // ignore
        }

        return ValueTask.FromResult(true);
    }

    [Command("CheckUpdateCommand")]
    private async Task CheckUpdateAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Check update", "SettingViewModel.Command"));

        await taskContext.SwitchToBackgroundAsync();

        CheckUpdateResult result = await updateService.CheckUpdateAsync().ConfigureAwait(false);
        await taskContext.InvokeOnMainThreadAsync(() => UpdateInfo = result.Kind switch
        {
            CheckUpdateResultKind.UpdateAvailable => SH.FormatViewModelSettingUpdateAvailable(result.PackageInformation?.Version.ToString()),
            CheckUpdateResultKind.AlreadyUpdated => SH.ViewModelSettingAlreadyUpdated,
            CheckUpdateResultKind.VersionApiInvalidResponse or CheckUpdateResultKind.VersionApiInvalidSha256 => SH.ViewModelSettingCheckUpdateFailed,
            _ => default!,
        }).ConfigureAwait(false);

        await updateService.TriggerUpdateAsync(result).ConfigureAwait(false);
    }

    [Command("RestartAsElevatedCommand")]
    private void RestartAsElevated()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Restart as elevated", "NotifyIconViewModel.Command"));
        NativeMethods.RestartAsAdministrator();
    }

    [Command("CreateDesktopShortcutCommand")]
    private void CreateDesktopShortcutForElevatedLaunchAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Create desktop shortcut for elevated launch", "SettingViewModel.Command"));

        _ = shellLinkInterop.TryCreateDesktopShortcutForElevatedLaunch()
            ? messenger.Send(InfoBarMessage.Success(SH.ViewModelSettingActionComplete))
            : messenger.Send(InfoBarMessage.Warning(SH.ViewModelSettingCreateDesktopShortcutFailed));
    }

}