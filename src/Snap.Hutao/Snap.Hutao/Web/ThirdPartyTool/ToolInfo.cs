using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Snap.Hutao.Web.ThirdPartyTool;

internal sealed class ToolInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("desc")]
    public string Description { get; set; } = default!;

    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = default!;

    [JsonPropertyName("is_compressed")]
    public bool IsCompressed { get; set; }

    [JsonPropertyName("main_exe")]
    public string? MainExe { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = default!;
}