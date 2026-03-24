using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.Factory.ContentDialog;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Service.ThirdPartyTool;
using Snap.Hutao.Web.ThirdPartyTool;

namespace Snap.Hutao.UI.Xaml.View.Dialog;

[DependencyProperty<ToolInfo>("Tool")]
[DependencyProperty<bool>("IsDownloading", DefaultValue = false)]
internal sealed partial class ThirdPartyToolDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IThirdPartyToolService thirdPartyToolService;
    private readonly IMessenger messenger;

    [GeneratedConstructor(InitializeComponent = true)]
    public partial ThirdPartyToolDialog(IServiceProvider serviceProvider);

    public ThirdPartyToolDialog(IServiceProvider serviceProvider, ToolInfo tool)
        : this(serviceProvider)
    {
        Tool = tool;
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        HandleLaunchAsync().SafeForget();
    }

    private async Task HandleLaunchAsync()
    {
        // 在 UI 线程上获取 Tool 的引用，避免后续跨线程访问依赖属性
        ToolInfo? tool = Tool;

        try
        {
            IsDownloading = true;

            if (tool is null)
            {
                return;
            }

            // 检查工具是否需要下载或更新
            bool needDownload = !thirdPartyToolService.IsToolDownloaded(tool) || thirdPartyToolService.NeedsUpdate(tool);

            if (needDownload)
            {
                // 下载工具
                bool downloadSuccess = await thirdPartyToolService.DownloadToolAsync(tool, null).ConfigureAwait(false);
                if (!downloadSuccess)
                {
                    await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
                    IsDownloading = false;
                    return;
                }
            }

            // 启动工具
            bool launchSuccess = await thirdPartyToolService.LaunchToolAsync(tool).ConfigureAwait(false);
            if (launchSuccess)
            {
                await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
                Hide();
                return;
            }
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
        }
        finally
        {
            await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
            IsDownloading = false;
        }
    }
}