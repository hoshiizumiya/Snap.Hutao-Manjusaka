using Snap.Hutao.Service.Yae.Achievement;

namespace Snap.Hutao.Service.Yae.Metadata;

internal sealed class YaeNativeLibConfig
{
    public required uint StoreCmdId { get; init; }

    public required uint AchievementCmdId { get; init; }

    public required IReadOnlyDictionary<uint, MethodRva> MethodRva { get; init; }
}
