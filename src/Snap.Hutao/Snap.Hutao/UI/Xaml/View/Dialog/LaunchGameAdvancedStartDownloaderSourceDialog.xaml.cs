// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.Core.Setting;
using Snap.Hutao.Factory.ContentDialog;
using Snap.Hutao.Service.Notification;

namespace Snap.Hutao.UI.Xaml.View.Dialog;

internal sealed partial class LaunchGameAdvancedStartDownloaderSourceDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    public LaunchGameAdvancedStartDownloaderSourceDialog(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        contentDialogFactory = serviceProvider.GetRequiredService<IContentDialogFactory>();
        taskContext = serviceProvider.GetRequiredService<ITaskContext>();
        messenger = serviceProvider.GetRequiredService<IMessenger>();
    }

    public async ValueTask<ValueResult<bool, string?>> GetResultAsync()
    {
        ContentDialogResult result = await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false);
        string? endpoint = LocalSetting.Get(SettingKeys.LaunchAdvancedStartFeedEndpoint, string.Empty);
        return new(result is ContentDialogResult.Primary, string.IsNullOrWhiteSpace(endpoint) ? null : endpoint);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        // Populate textbox with current setting
        string endpoint = LocalSetting.Get(SettingKeys.LaunchAdvancedStartFeedEndpoint, string.Empty);
        ViewDialogLaunchGameAdvancedStartSourceSetterEndpointTextBox.Text = endpoint;
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        ContentDialogButtonClickDeferral def = args.GetDeferral();

        try
        {
            string? text = ViewDialogLaunchGameAdvancedStartSourceSetterEndpointTextBox.Text;
            LocalSetting.Set(SettingKeys.LaunchAdvancedStartFeedEndpoint, text ?? string.Empty);
            await taskContext.SwitchToMainThreadAsync();
            messenger.Send(InfoBarMessage.Success(SH.ViewDialogLaunchGameAdvancedStartSourceSetterSaveSuccess));
        }
        finally
        {
            def.Complete();
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        LocalSetting.Set(SettingKeys.LaunchAdvancedStartFeedEndpoint, "https://hutaofeed.pages.dev/programs.json");
        ViewDialogLaunchGameAdvancedStartSourceSetterEndpointTextBox.Text = "https://hutaofeed.pages.dev/programs.json";
        messenger.Send(InfoBarMessage.Success(SH.ViewDialogLaunchGameAdvancedStartSourceSetterSaveSuccess));

    }
}
