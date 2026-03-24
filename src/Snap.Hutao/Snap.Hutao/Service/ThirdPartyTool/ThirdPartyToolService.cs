using Snap.Hutao.Core;
using Snap.Hutao.Core.DependencyInjection.Annotation.HttpClient;
using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Web.Request.Builder;
using Snap.Hutao.Web.Request.Builder.Abstraction;
using Snap.Hutao.Web.ThirdPartyTool;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace Snap.Hutao.Service.ThirdPartyTool;

[HttpClient(HttpClientConfiguration.Default)]
[Service(ServiceLifetime.Singleton, typeof(IThirdPartyToolService))]
internal sealed partial class ThirdPartyToolService : IThirdPartyToolService
{
    private const string ApiBaseUrl = "https://htserver.wdg12.work/api";
    private const string ToolsEndpoint = "/tools";
    private const string ToolInfoFileName = "tool_info.json";

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IMessenger messenger;

    [GeneratedConstructor]
    public partial ThirdPartyToolService(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<ImmutableArray<ToolInfo>> GetToolsAsync(CancellationToken token = default)
    {
        try
        {
            HttpClient httpClient = httpClientFactory.CreateClient();

            // 添加日志
            SentrySdk.AddBreadcrumb($"Creating request to: {ApiBaseUrl}{ToolsEndpoint}", category: "ThirdPartyTool");

            HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
                .SetRequestUri($"{ApiBaseUrl}{ToolsEndpoint}")
                .Get();

            SentrySdk.AddBreadcrumb($"Sending HTTP request", category: "ThirdPartyTool");

            ToolApiResponse? response = await builder
                .SendAsync<ToolApiResponse>(httpClient, token)
                .ConfigureAwait(false);

            SentrySdk.AddBreadcrumb($"Request completed", category: "ThirdPartyTool");

            if (response is null)
            {
                SentrySdk.AddBreadcrumb("Response is null", category: "ThirdPartyTool");
                return ImmutableArray<ToolInfo>.Empty;
            }

            SentrySdk.AddBreadcrumb($"Response received: Code={response.Code}, Message={response.Message}, Data.Length={response.Data.Length}", category: "ThirdPartyTool");

            if (response.Code != 0)
            {
                SentrySdk.AddBreadcrumb($"API returned error code: {response.Code}, Message: {response.Message}", category: "ThirdPartyTool");
                return ImmutableArray<ToolInfo>.Empty;
            }

            return response.Data;
        }
        catch (HttpRequestException ex)
        {
            SentrySdk.AddBreadcrumb($"HTTP request failed: {ex.Message}", category: "ThirdPartyTool");
            SentrySdk.CaptureException(ex);
            return ImmutableArray<ToolInfo>.Empty;
        }
        catch (TaskCanceledException ex)
        {
            SentrySdk.AddBreadcrumb($"Request timed out or was cancelled: {ex.Message}", category: "ThirdPartyTool");
            SentrySdk.CaptureException(ex);
            return ImmutableArray<ToolInfo>.Empty;
        }
        catch (Exception ex)
        {
            SentrySdk.AddBreadcrumb($"Failed to get third party tools: {ex.Message}", category: "ThirdPartyTool");
            SentrySdk.CaptureException(ex);
            return ImmutableArray<ToolInfo>.Empty;
        }
    }

    public async ValueTask<bool> DownloadToolAsync(ToolInfo tool, IProgress<double>? progress = null, CancellationToken token = default)
    {
        try
        {
            string toolDirectory = GetToolDirectory(tool);

            // 如果需要更新，先清理旧文件
            if (NeedsUpdate(tool) && Directory.Exists(toolDirectory))
            {
                Directory.Delete(toolDirectory, true);
            }

            Directory.CreateDirectory(toolDirectory);

            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
                if (tool.IsCompressed)
                {
                    // 压缩包模式：下载并解压
                    await DownloadAndExtractCompressedToolAsync(httpClient, tool, toolDirectory, progress, token).ConfigureAwait(false);
                }
                else
                {
                    // 非压缩包模式：直接下载所有文件
                    await DownloadFilesAsync(httpClient, tool, toolDirectory, progress, token).ConfigureAwait(false);
                }
            }

            // 保存本地工具信息
            SaveLocalToolInfo(tool);

            return true;
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
            return false;
        }
    }

    public async ValueTask<bool> LaunchToolAsync(ToolInfo tool)
    {
        try
        {
            string toolDirectory = GetToolDirectory(tool);

            // 优先使用 main_exe，如果没有则查找可执行文件
            string? executablePath = tool.MainExe;
            if (string.IsNullOrEmpty(executablePath))
            {
                executablePath = tool.Files.FirstOrDefault(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            }

            // 如果还是没有，尝试从目录中查找
            if (string.IsNullOrEmpty(executablePath))
            {
                string[] exeFiles = Directory.GetFiles(toolDirectory, "*.exe", SearchOption.TopDirectoryOnly);
                executablePath = exeFiles.FirstOrDefault();
            }

            if (string.IsNullOrEmpty(executablePath))
            {
                messenger.Send(InfoBarMessage.Warning(SH.ServiceThirdPartyToolNoExecutableFound));
                return false;
            }

            // 如果 executablePath 是完整路径，直接使用；否则拼接目录
            string fullPath = Path.IsPathRooted(executablePath)
                ? executablePath
                : Path.Combine(toolDirectory, Path.GetFileName(executablePath));

            if (!File.Exists(fullPath))
            {
                messenger.Send(InfoBarMessage.Warning(SH.FormatServiceThirdPartyToolFileNotFound(fullPath)));
                return false;
            }

            // 尝试以管理员权限启动
            ProcessStartInfo startInfo = new()
            {
                FileName = fullPath,
                WorkingDirectory = toolDirectory,
                UseShellExecute = true,
                Verb = "runas", // 请求管理员权限
            };

            try
            {
                Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // 用户拒绝了管理员权限，尝试以普通权限启动
                startInfo.Verb = string.Empty;
                startInfo.UseShellExecute = false;
                Process.Start(startInfo);
            }

            return true;
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
            return false;
        }
    }

    public bool IsToolDownloaded(ToolInfo tool)
    {
        string toolDirectory = GetToolDirectory(tool);
        if (!Directory.Exists(toolDirectory))
        {
            return false;
        }

        // 检查工具信息文件是否存在
        LocalToolInfo? localInfo = GetLocalToolInfo(tool);
        if (localInfo is null)
        {
            return false;
        }

        // 对于压缩包，检查目录是否有内容
        if (tool.IsCompressed)
        {
            return Directory.GetFiles(toolDirectory, "*", SearchOption.AllDirectories).Length > 0;
        }

        // 对于非压缩包，检查所有文件是否存在
        foreach (string fileName in tool.Files)
        {
            string filePath = Path.Combine(toolDirectory, fileName);
            if (!File.Exists(filePath))
            {
                return false;
            }
        }

        return true;
    }

    public LocalToolInfo? GetLocalToolInfo(ToolInfo tool)
    {
        string toolDirectory = GetToolDirectory(tool);
        string infoFilePath = Path.Combine(toolDirectory, ToolInfoFileName);

        if (!File.Exists(infoFilePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(infoFilePath);
            return JsonSerializer.Deserialize<LocalToolInfo>(json);
        }
        catch
        {
            return null;
        }
    }

    public bool NeedsUpdate(ToolInfo tool)
    {
        LocalToolInfo? localInfo = GetLocalToolInfo(tool);
        if (localInfo is null)
        {
            return true; // 没有本地信息，需要下载
        }

        // 比较版本号
        return IsNewerVersion(tool.Version, localInfo.Version);
    }

    private static async Task DownloadAndExtractCompressedToolAsync(
        HttpClient httpClient,
        ToolInfo tool,
        string toolDirectory,
        IProgress<double>? progress,
        CancellationToken token)
    {
        // 压缩包模式通常只有一个 zip 文件
        string zipFileName = tool.Files.FirstOrDefault(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? tool.Files[0];

        string zipUrl = $"{tool.Url}{zipFileName}";
        string zipFilePath = Path.Combine(toolDirectory, zipFileName);

        // 下载 zip 文件
        progress?.Report(0);

        HttpResponseMessage response = await httpClient.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using (Stream contentStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
        using (FileStream fileStream = new(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await contentStream.CopyToAsync(fileStream, token).ConfigureAwait(false);
        }

        progress?.Report(50);

        // 解压 zip 文件
        using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
        {
            // 检查是否有根目录需要处理
            bool hasRootFolder = HasSingleRootFolder(archive);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    // 这是一个目录，创建它
                    string? destinationPath = GetDestinationPath(entry.FullName, toolDirectory, hasRootFolder);
                    if (destinationPath is not null)
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    continue;
                }

                string? destFilePath = GetDestinationPath(entry.FullName, toolDirectory, hasRootFolder);
                if (destFilePath is null)
                {
                    continue;
                }

                // 确保目录存在
                string? destDir = Path.GetDirectoryName(destFilePath);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                entry.ExtractToFile(destFilePath, true);
            }
        }

        progress?.Report(90);

        // 删除 zip 文件
        File.Delete(zipFilePath);

        progress?.Report(100);
    }

    private static async Task DownloadFilesAsync(
        HttpClient httpClient,
        ToolInfo tool,
        string toolDirectory,
        IProgress<double>? progress,
        CancellationToken token)
    {
        int totalFiles = tool.Files.Count;
        int downloadedFiles = 0;

        foreach (string fileName in tool.Files)
        {
            string fileUrl = $"{tool.Url}{fileName}";
            string localFilePath = Path.Combine(toolDirectory, fileName);

            // 如果文件已存在，跳过下载
            if (File.Exists(localFilePath))
            {
                downloadedFiles++;
                progress?.Report((double)downloadedFiles / totalFiles * 100);
                continue;
            }

            // 下载文件
            HttpResponseMessage response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using (Stream contentStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            using (FileStream fileStream = new(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await contentStream.CopyToAsync(fileStream, token).ConfigureAwait(false);
            }

            downloadedFiles++;
            progress?.Report((double)downloadedFiles / totalFiles * 100);
        }
    }

    private void SaveLocalToolInfo(ToolInfo tool)
    {
        string toolDirectory = GetToolDirectory(tool);
        string infoFilePath = Path.Combine(toolDirectory, ToolInfoFileName);

        LocalToolInfo localInfo = LocalToolInfo.FromToolInfo(tool);
        string json = JsonSerializer.Serialize(localInfo, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(infoFilePath, json);
    }

    private static bool IsNewerVersion(string remoteVersion, string localVersion)
    {
        // 使用 Version 类进行比较
        if (Version.TryParse(remoteVersion, out Version? remote) && Version.TryParse(localVersion, out Version? local))
        {
            return remote > local;
        }

        // 如果无法解析为版本号，进行字符串比较
        return !string.Equals(remoteVersion, localVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSingleRootFolder(ZipArchive archive)
    {
        // 检查是否所有条目都在同一个根目录下
        HashSet<string> rootFolders = [];
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            int separatorIndex = entry.FullName.IndexOf('/');
            if (separatorIndex > 0)
            {
                rootFolders.Add(entry.FullName[..separatorIndex]);
            }
            else if (separatorIndex < 0 && !string.IsNullOrEmpty(entry.Name))
            {
                // 直接在根目录下的文件
                return false;
            }
        }

        return rootFolders.Count == 1;
    }

    private static string? GetDestinationPath(string entryPath, string toolDirectory, bool hasRootFolder)
    {
        if (string.IsNullOrEmpty(entryPath))
        {
            return null;
        }

        if (hasRootFolder)
        {
            // 移除根目录前缀
            int separatorIndex = entryPath.IndexOf('/');
            if (separatorIndex >= 0 && separatorIndex < entryPath.Length - 1)
            {
                return Path.Combine(toolDirectory, entryPath[(separatorIndex + 1)..]);
            }
            else if (separatorIndex >= 0)
            {
                // 这是根目录本身
                return null;
            }
        }

        return Path.Combine(toolDirectory, entryPath);
    }

    private static string GetToolDirectory(ToolInfo tool)
    {
        // 使用数据目录/工具名作为存储路径
        return Path.Combine(HutaoRuntime.DataDirectory, "Tools", tool.Name);
    }
}
