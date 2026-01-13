using Snap.Hutao.Web.ThirdPartyTool;
using System.Collections.Immutable;

namespace Snap.Hutao.Service.ThirdPartyTool;

internal interface IThirdPartyToolService
{
    /// <summary>
    /// 获取第三方工具列表
    /// </summary>
    /// <param name="token">取消令牌</param>
    /// <returns>工具列表</returns>
    ValueTask<ImmutableArray<ToolInfo>> GetToolsAsync(CancellationToken token = default);

    /// <summary>
    /// 下载工具文件
    /// </summary>
    /// <param name="tool">工具信息</param>
    /// <param name="progress">进度报告</param>
    /// <param name="token">取消令牌</param>
    /// <returns>是否下载成功</returns>
    ValueTask<bool> DownloadToolAsync(ToolInfo tool, IProgress<double>? progress = null, CancellationToken token = default);

    /// <summary>
    /// 启动工具
    /// </summary>
    /// <param name="tool">工具信息</param>
    /// <returns>是否启动成功</returns>
    ValueTask<bool> LaunchToolAsync(ToolInfo tool);

    /// <summary>
    /// 检查工具是否已下载
    /// </summary>
    /// <param name="tool">工具信息</param>
    /// <returns>是否已下载</returns>
    bool IsToolDownloaded(ToolInfo tool);
}