// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.Logging;
using Snap.Hutao.Core.Property;
using Snap.Hutao.Factory.ContentDialog;
using Snap.Hutao.Core.Setting;
using Snap.Hutao.Model;
using Snap.Hutao.Model.Entity;
using Snap.Hutao.Model.Intrinsic;
using Snap.Hutao.Service.Game;
using Snap.Hutao.Service.Game.FileSystem;
using Snap.Hutao.Service.Game.Locator;
using Snap.Hutao.Service.Game.Package;
using Snap.Hutao.Service.Game.PathAbstraction;
using Snap.Hutao.Service.Game.Scheme;
using Snap.Hutao.Service.Navigation;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Service.User;
using Snap.Hutao.Factory.Picker;
using Snap.Hutao.Factory.Process;
using Windows.System;
using Snap.Hutao.UI.Input.LowLevel;
using Snap.Hutao.UI.Xaml.View.Dialog;
using Snap.Hutao.UI.Xaml.View.Window;
using Snap.Hutao.ViewModel.User;
using System.Collections.Immutable;
using System.IO;
using Snap.Hutao.Core.IO;
using System.Collections.ObjectModel;
using Snap.Hutao.Service.Game.AdvancedStart;
using Snap.Hutao.Service.Game.AdvancedStart.Model;

namespace Snap.Hutao.ViewModel.Game;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Singleton)]
internal sealed partial class LaunchGameViewModel : Abstraction.ViewModel, IViewModelSupportLaunchExecution, INavigationRecipient
{
    private readonly IGameLocatorFactory gameLocatorFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly IGameService gameService;
    private readonly IUserService userService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;
    private readonly IFileSystemPickerInteraction fileSystemPickerInteraction;
    private readonly AdvancedStartDelayedProgramStore store;

    [GeneratedConstructor]
    public partial LaunchGameViewModel(IServiceProvider serviceProvider);

    public partial GamePackageInstallViewModel GamePackageInstallViewModel { get; }

    public partial GamePackageViewModel GamePackageViewModel { get; }

    public partial LaunchStatusOptions LaunchStatusOptions { get; }

    public partial LowLevelKeyOptions LowLevelKeyOptions { get; }

    public partial LaunchOptions LaunchOptions { get; }

    public partial LaunchGameShared Shared { get; }

    public ImmutableArray<LaunchScheme> KnownSchemes { get; } = KnownLaunchSchemes.Values;

    public string AdvancedStartProgramPath
    {
        get => field;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    LaunchScheme? IViewModelSupportLaunchExecution.TargetScheme { get => TargetSchemeFilteredGameAccountsView.Scheme; }

    LaunchScheme? IViewModelSupportLaunchExecution.CurrentScheme { get => Shared.GetCurrentLaunchSchemeFromConfigurationFile(); }

    GameAccount? IViewModelSupportLaunchExecution.GameAccount { get => TargetSchemeFilteredGameAccountsView.View?.CurrentItem; }

    public LaunchSchemeFilteredGameAccountsView TargetSchemeFilteredGameAccountsView { get => field ??= new(IsViewUnloaded, gameService, taskContext, messenger); private set; }

    public IObservableProperty<NameValue<PlatformType>?> SelectedPlatformType { get => field ??= LaunchOptions.PlatformType.AsNameValue(LaunchOptions.PlatformTypes); }

    public IObservableProperty<GamePathEntry?> GamePathEntry { get => field ??= LaunchOptions.GamePathEntry.SetWithCondition(static (value, unloaded) => !unloaded.Value && value is not null, IsViewUnloaded); }

    public IReadOnlyObservableProperty<string> DisplayGamePath { get => field ??= Property.Observe(LaunchOptions.GamePathEntry, static entry => SH.FormatViewModelLaunchGameDisplayGamePath(entry?.Path)); }

    public IReadOnlyObservableProperty<bool> GamePathEntryValid { get => field ??= Property.Observe(LaunchOptions.GamePathEntry, static entry => !string.IsNullOrEmpty(entry?.Path)).WithValueChangedCallback(static (v, vm) => vm.HandleGamePathEntryChangeAsync().SafeForget(), this); }

    public IReadOnlyObservableProperty<bool> IsIslandConnected { get => GameLifeCycle.IsIslandConnected.AsReadOnly(); }

    // Delayed programs
    public ObservableCollection<AdvancedStartDelayedProgramEntry> Entries { get; private set => SetProperty(ref field, value); } = [];

    private AdvancedStartDelayedProgramEntry? selectedDelayedProgramEntry;

    public AdvancedStartDelayedProgramEntry? SelectedDelayedProgramEntry
    {
        get => selectedDelayedProgramEntry;
        set => SetProperty(ref selectedDelayedProgramEntry, value);
    }

    public async ValueTask<bool> ReceiveAsync(INavigationExtraData data, CancellationToken token)
    {
        if (!await Initialization.Task.ConfigureAwait(false))
        {
            return false;
        }

        if (data is LaunchGameExtraData { TypedData: { } uid })
        {
            return await userService.SetCurrentUserByUidAsync(uid).ConfigureAwait(false);
        }

        return false;
    }

    [SuppressMessage("", "SH003")]
    public async Task HandleGamePathEntryChangeAsync()
    {
        try
        {
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                LaunchScheme? currentScheme = GamePathEntry.Value is not null
                    ? Shared.GetCurrentLaunchSchemeFromConfigurationFile()
                    : default;

                await taskContext.SwitchToMainThreadAsync();
                await TargetSchemeFilteredGameAccountsView.SetAsync(currentScheme).ConfigureAwait(true);
                await GamePackageViewModel.ReloadAsync().ConfigureAwait(true);
            }
        }
        catch (HutaoException ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
        }
    }

    ValueTask<BlockDeferral<PackageConvertStatus>> IViewModelSupportLaunchExecution.CreateConvertBlockDeferralAsync()
    {
        return BlockDeferral<PackageConvertStatus>.CreateAsync<LaunchGamePackageConvertDialog>(serviceProvider, static (state, dialog) => dialog.State = state);
    }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        AdvancedStartProgramPath = LocalSetting.Get(SettingKeys.LaunchAdvancedStartProgramPath, string.Empty);

        // Load delayed program entries
        try
        {
            Entries = store.Load();
        }
        catch
        {
            Entries = [];
        }

        if (LaunchOptions.GamePathEntries.Value.IsDefaultOrEmpty)
        {
            await serviceProvider.GetRequiredService<IGamePathService>().SilentLocateAllGamePathAsync().ConfigureAwait(false);
        }

        await HandleGamePathEntryChangeAsync().ConfigureAwait(false);
        Shared.ResumeLaunchExecutionAsync(this).SafeForget();
        return true;
    }

    [Command("IdentifyMonitorsCommand")]
    private static async Task IdentifyMonitorsAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Identify monitors", "LaunchGameViewModel.Command"));
        await IdentifyMonitorWindow.IdentifyAllMonitorsAsync(3).ConfigureAwait(false);
    }

    [Command("PickGamePathCommand")]
    private async Task PickGamePathAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Set game path by picker", "LaunchGameViewModel.Command"));
        if (await gameLocatorFactory.LocateSingleAsync(GameLocationSourceKind.Manual).ConfigureAwait(false) is not (true, var path))
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();
        LaunchOptions.PerformGamePathEntrySynchronization(path);
    }

    [Command("ResetGamePathCommand")]
    private void ResetGamePath()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Reset game path", "LaunchGameViewModel.Command"));
        LaunchOptions.GamePathEntry.Value = default;
        _ = 1;
    }

    [Command("RemoveGamePathEntryCommand")]
    private void RemoveGamePathEntry(GamePathEntry? entry)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Remove game path", "LaunchGameViewModel.Command"));
        LaunchOptions.RemoveGamePathEntry(entry);
    }

    [Command("RemoveAspectRatioCommand")]
    private void RemoveAspectRatio(AspectRatio? aspectRatio)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Remove aspect ratio", "LaunchGameViewModel.Command"));
        if (aspectRatio is null)
        {
            return;
        }

        if (aspectRatio.Equals(LaunchOptions.SelectedAspectRatio))
        {
            LaunchOptions.SelectedAspectRatio = default;
        }

        LaunchOptions.AspectRatios.Remove(aspectRatio);
    }

    [Command("LaunchCommand")]
    private async Task LaunchAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Launch game", "LaunchGameViewModel.Command"));

        UserAndUid? userAndUid = await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false);
        await Shared.DefaultLaunchExecutionAsync(this, userAndUid).ConfigureAwait(false);
    }

    [Command("ConvertCommand")]
    private async Task ConvertAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Convert game server", "LaunchGameViewModel.Command"));
        await Shared.ConvertLaunchExecutionAsync(this).ConfigureAwait(false);
    }

    [Command("DetectGameAccountCommand")]
    private async Task DetectGameAccountAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Detect registry game account", "LaunchGameViewModel.Command"));

        try
        {
            if (TargetSchemeFilteredGameAccountsView.Scheme is null)
            {
                messenger.Send(InfoBarMessage.Error(SH.ViewModelLaunchGameSchemeNotSelected));
                return;
            }

            if (TargetSchemeFilteredGameAccountsView.View is null)
            {
                return;
            }

            GameAccount? currentAccount = await gameService.DetectGameAccountAsync(TargetSchemeFilteredGameAccountsView.Scheme, async (suggestedName) =>
            {
                using (IServiceScope scope = serviceProvider.CreateScope())
                {
                    LaunchGameAccountNameDialog dialog = await scope.ServiceProvider
                        .GetRequiredService<IContentDialogFactory>()
                        .CreateInstanceAsync<LaunchGameAccountNameDialog>(scope.ServiceProvider, suggestedName)
                        .ConfigureAwait(false);
                    return await dialog.GetInputNameAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            if (currentAccount is not null)
            {
                await taskContext.SwitchToMainThreadAsync();
                TargetSchemeFilteredGameAccountsView.View.MoveCurrentTo(currentAccount);
            }
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
        }
    }

    [Command("ModifyGameAccountCommand")]
    private async Task ModifyGameAccountAsync(GameAccount? gameAccount)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Modify registry game account", "LaunchGameViewModel.Command"));

        if (gameAccount is null)
        {
            return;
        }

        await gameService.ModifyGameAccountAsync(gameAccount, async originalName =>
        {
            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                LaunchGameAccountNameDialog dialog = await scope.ServiceProvider
                    .GetRequiredService<IContentDialogFactory>()
                    .CreateInstanceAsync<LaunchGameAccountNameDialog>(scope.ServiceProvider, originalName)
                    .ConfigureAwait(false);

                return await dialog.GetInputNameAsync().ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    [Command("RemoveGameAccountCommand")]
    private async Task RemoveGameAccountAsync(GameAccount? gameAccount)
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Remove registry game account", "LaunchGameViewModel.Command"));

        if (gameAccount is null)
        {
            return;
        }

        await gameService.RemoveGameAccountAsync(gameAccount).ConfigureAwait(false);
    }

    [Command("OpenScreenshotFolderCommand")]
    private async Task OpenScreenshotFolderAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Open screenshot folder", "LaunchGameViewModel.Command"));

        const string LockTrace = $"{nameof(LaunchGameViewModel)}.{nameof(OpenScreenshotFolderAsync)}";
        if (LaunchOptions.TryGetGameFileSystem(LockTrace, out IGameFileSystem? gameFileSystem) is not GameFileSystemErrorKind.None)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(gameFileSystem);
        using (gameFileSystem)
        {
            Directory.CreateDirectory(gameFileSystem.ScreenShotDirectory);
            await Windows.System.Launcher.LaunchFolderPathAsync(gameFileSystem.ScreenShotDirectory);
        }
    }

    [Command("KillGameProcessCommand")]
    private async Task KillGameProcess()
    {
        if (!LaunchOptions.CanKillGameProcess.Value)
        {
            return;
        }

        await GameLifeCycle.TryKillGameProcessAsync(taskContext).ConfigureAwait(false);
    }

    [Command("LaunchAdvancedCommand")]
    private async Task LaunchAdvancedAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Launch advanced start program", "LaunchGameViewModel.Command"));

        try
        {
            string programPath = LocalSetting.Get(SettingKeys.LaunchAdvancedStartProgramPath, string.Empty);
            if (string.IsNullOrWhiteSpace(programPath))
            {
                messenger.Send(InfoBarMessage.Warning(SH.ViewModelLaunchGameAdvancedStartProgramPathNotSet));
                return;
            }

            if (!File.Exists(programPath))
            {
                messenger.Send(InfoBarMessage.Error(SH.ViewModelLaunchGameAdvancedStartProgramNotExists, programPath));
                return;
            }

            // Start using shell execute (no arguments)
            ProcessFactory.StartUsingShellExecute(string.Empty, programPath);
            messenger.Send(InfoBarMessage.Success(SH.ViewModelLaunchGameAdvancedStartProgramLaunched));
        }
        catch (Exception ex)
        {
            // For UAC user cancel it's a ex too, need a way to...
            messenger.Send(InfoBarMessage.Error(ex));
        }
    }

    [Command("LaunchAdvancedDelayedCommand")]
    private async Task LaunchAdvancedDelayedAsync()
    {
        // Run each delayed program sequentially with its configured delay.
        IReadOnlyList<AdvancedStartDelayedProgramEntry> snapshot = Entries.ToList();

        foreach (AdvancedStartDelayedProgramEntry entry in snapshot)
        {
            CancellationToken.ThrowIfCancellationRequested();

            int delaySeconds = Math.Max(0, entry.DelaySeconds);
            if (delaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), CancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(entry.Path) || !File.Exists(entry.Path))
            {
                messenger.Send(InfoBarMessage.Error(SH.ViewModelLaunchGameAdvancedStartProgramNotExists, entry.Path));
                continue;
            }

            try
            {
                ProcessFactory.StartUsingShellExecute(string.Empty, entry.Path);
            }
            catch (Exception ex)
            {
                messenger.Send(InfoBarMessage.Error(ex));
            }
        }
    }

    [Command("PickAdvancedStartProgramPathCommand")]
    private async Task PickAdvancedStartProgramPathAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Pick advanced start program", "LaunchGameViewModel.Command"));

        await taskContext.SwitchToBackgroundAsync();
        (bool picked, ValueFile file) = fileSystemPickerInteraction.PickFile(
            "Picker",
            "program",
            "*.exe");

        if (!picked)
        {
            return;
        }

        string path = file;

        // Persist can be done off-thread; UI-bound property update must be on UI thread.
        LocalSetting.Set(SettingKeys.LaunchAdvancedStartProgramPath, path);

        await taskContext.SwitchToMainThreadAsync();
        AdvancedStartProgramPath = path;
        messenger.Send(InfoBarMessage.Success(SH.ViewModelLaunchGameAdvancedStartProgramPathSaved));
    }

    [Command("OpenAdvancedStartCommunityCommand")]
    private static async Task OpenAdvancedStartCommunityAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Open advanced start community", "LaunchGameViewModel.Command"));

        // TODO: replace with official page if needed.
        await Launcher.LaunchUriAsync("https://github.com/hoshiizumiya/Snap.Hutao-Manjusaka/discussions".ToUri());
    }

    [Command("OpenAdvancedStartCheckDownloadCommand")]
    private async Task OpenAdvancedStartCheckDownloadAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Open advanced start downloader", "LaunchGameViewModel.Command"));

        try
        {
            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                IContentDialogFactory dialogFactory = scope.ServiceProvider.GetRequiredService<IContentDialogFactory>();
                LaunchGameAdvancedStartDownloadDialog dialog = await dialogFactory.CreateInstanceAsync<LaunchGameAdvancedStartDownloadDialog>(scope.ServiceProvider).ConfigureAwait(false);

                // Show dialog and get result
                (bool ok, string? programPath) = await dialog.GetResultAsync().ConfigureAwait(false);
                if (!ok || string.IsNullOrWhiteSpace(programPath))
                {
                    return;
                }

                // Persist and update UI on main thread
                LocalSetting.Set(SettingKeys.LaunchAdvancedStartProgramPath, programPath);
                await taskContext.SwitchToMainThreadAsync();
                AdvancedStartProgramPath = programPath;
                messenger.Send(InfoBarMessage.Success(SH.ViewModelLaunchGameAdvancedStartProgramPathSaved));
            }
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
        }
    }

    [Command("OpenAdvancedStartListSourceSetterCommand")]
    private async Task OpenAdvancedStartListSourceSetterAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Open advanced start LaunchGameAdvancedStartDownloaderSourceDialog", "LaunchGameViewModel.Command"));

        try
        {
            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                IContentDialogFactory dialogFactory = scope.ServiceProvider.GetRequiredService<IContentDialogFactory>();
                LaunchGameAdvancedStartDownloaderSourceDialog dialog = await dialogFactory.CreateInstanceAsync<LaunchGameAdvancedStartDownloaderSourceDialog>(scope.ServiceProvider).ConfigureAwait(false);

                // Show dialog and get result
                (bool ok, string? programPath) = await dialog.GetResultAsync().ConfigureAwait(false);
                if (!ok || string.IsNullOrWhiteSpace(programPath))
                {
                    return;
                }

                // Persist and update UI on main thread
                LocalSetting.Set(SettingKeys.LaunchAdvancedStartProgramPath, programPath);
                await taskContext.SwitchToMainThreadAsync();
                AdvancedStartProgramPath = programPath;
                messenger.Send(InfoBarMessage.Success(SH.ViewModelLaunchGameAdvancedStartProgramPathSaved));
            }
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
        }
    }

    // Delayed Programs Commands
    [Command("AddDelayedProgramCommand")]
    private async Task AddDelayedProgramAsync()
    {
        await taskContext.SwitchToBackgroundAsync();
        (bool ok, ValueFile file) = fileSystemPickerInteraction.PickFile("Picker", "program", "*.exe");
        if (!ok)
        {
            return;
        }

        string path = file;
        string name = Path.GetFileNameWithoutExtension(path);

        await taskContext.SwitchToMainThreadAsync();
        AdvancedStartDelayedProgramEntry entry = new(name, path, 0);
        Entries.Add(entry);
        SelectedDelayedProgramEntry = entry;
        store.Save(Entries);
    }

    [Command("RemoveDelayedProgramCommand")]
    private void RemoveDelayedProgram()
    {
        if (SelectedDelayedProgramEntry is null)
        {
            return;
        }

        Entries.Remove(SelectedDelayedProgramEntry);
        SelectedDelayedProgramEntry = null;
        store.Save(Entries);
    }

    [Command("SaveDelayedProgramCommand")]
    private void SaveDelayedProgram()
    {
        store.Save(Entries);
        messenger.Send(InfoBarMessage.Success(SH.ViewModelLaunchGameAdvancedStartProgramPathSaved));
    }

    [Command("EditDelayedProgramCommand")]
    private Task EditDelayedProgramAsync()
    {
        return PickDelayedProgramPathAsync(SelectedDelayedProgramEntry);
    }

    [Command("PickDelayedProgramPathCommand")]
    private async Task PickDelayedProgramPathAsync(AdvancedStartDelayedProgramEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        await taskContext.SwitchToBackgroundAsync();
        (bool ok, ValueFile file) = fileSystemPickerInteraction.PickFile("Picker", "program", "*.exe");
        if (!ok)
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();
        entry.Path = file;
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            entry.Name = Path.GetFileNameWithoutExtension(entry.Path);
        }

        store.Save(Entries);
    }
}