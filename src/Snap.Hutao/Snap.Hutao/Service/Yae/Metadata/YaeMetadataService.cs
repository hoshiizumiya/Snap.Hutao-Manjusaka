using Microsoft.Extensions.Caching.Memory;
using Snap.Hutao.Core.DependencyInjection.Annotation.HttpClient;
using System.IO;
using System.Net.Http;

namespace Snap.Hutao.Service.Yae.Metadata;

[Service(ServiceLifetime.Singleton, typeof(IYaeMetadataService))]
[HttpClient(HttpClientConfiguration.Default)]
internal sealed partial class YaeMetadataService : IYaeMetadataService
{
    private const string MetadataUrl = "https://rin.holohat.work/schicksal/metadata";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
    private static readonly string? LocalMetadataPath = TryGetLocalMetadataPath();

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache memoryCache;

    [GeneratedConstructor]
    public partial YaeMetadataService(IServiceProvider serviceProvider);

    public ValueTask<YaeNativeLibConfig?> GetNativeLibConfigAsync(CancellationToken token = default)
    {
        Task<YaeNativeLibConfig?> task = memoryCache.GetOrCreateAsync($"{nameof(YaeMetadataService)}.NativeLibConfig", async entry =>
        {
            entry.SetSlidingExpiration(CacheDuration);

            byte[] data;
            if (!string.IsNullOrEmpty(LocalMetadataPath) && File.Exists(LocalMetadataPath))
            {
                data = await File.ReadAllBytesAsync(LocalMetadataPath, token).ConfigureAwait(false);
                if (data.Length > 0)
                {
                    return YaeMetadataParser.ParseNativeLibConfig(data);
                }
            }

            using HttpClient httpClient = httpClientFactory.CreateClient(nameof(YaeMetadataService));
            data = await httpClient.GetByteArrayAsync(MetadataUrl, token).ConfigureAwait(false);
            return YaeMetadataParser.ParseNativeLibConfig(data);
        });

        return new ValueTask<YaeNativeLibConfig?>(task);
    }

    private static string? TryGetLocalMetadataPath()
    {
        try
        {
            // 尝试获取用户下载目录下的metadata文件，本地测试和排查问题时使用
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile))
            {
                return default;
            }

            string localPath = Path.Combine(userProfile, "Downloads", "metadata");
            return localPath;
        }
        catch
        {
            return default;
        }
    }
}
