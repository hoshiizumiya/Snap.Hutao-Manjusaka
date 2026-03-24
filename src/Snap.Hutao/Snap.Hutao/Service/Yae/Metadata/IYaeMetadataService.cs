namespace Snap.Hutao.Service.Yae.Metadata;

internal interface IYaeMetadataService
{
    ValueTask<YaeNativeLibConfig?> GetNativeLibConfigAsync(CancellationToken token = default);
}
