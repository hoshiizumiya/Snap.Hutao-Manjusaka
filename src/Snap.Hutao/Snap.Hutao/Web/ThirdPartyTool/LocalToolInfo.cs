using System.Text.Json.Serialization;

namespace Snap.Hutao.Web.ThirdPartyTool;

/// <summary>
/// 本地工具信息，用于保存工具的本地状态
/// </summary>
internal sealed class LocalToolInfo
{
    /// <summary>
    /// 工具名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    /// <summary>
    /// 工具版本号
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = default!;

    /// <summary>
    /// 是否为压缩包
    /// </summary>
    [JsonPropertyName("is_compressed")]
    public bool IsCompressed { get; set; }

    /// <summary>
    /// 主可执行文件名
    /// </summary>
    [JsonPropertyName("main_exe")]
    public string? MainExe { get; set; }

    /// <summary>
    /// 下载时间
    /// </summary>
    [JsonPropertyName("download_time")]
    public DateTimeOffset DownloadTime { get; set; }

    /// <summary>
    /// 从 ToolInfo 创建 LocalToolInfo
    /// </summary>
    public static LocalToolInfo FromToolInfo(ToolInfo toolInfo)
    {
        return new LocalToolInfo
        {
            Name = toolInfo.Name,
            Version = toolInfo.Version,
            IsCompressed = toolInfo.IsCompressed,
            MainExe = toolInfo.MainExe,
            DownloadTime = DateTimeOffset.Now,
        };
    }
}
