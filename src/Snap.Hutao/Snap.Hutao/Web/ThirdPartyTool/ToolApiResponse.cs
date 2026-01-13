using Snap.Hutao.Web.Response;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Snap.Hutao.Web.ThirdPartyTool;

internal sealed class ToolApiResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;

    [JsonPropertyName("data")]
    public ImmutableArray<ToolInfo> Data { get; set; } = ImmutableArray<ToolInfo>.Empty;
}