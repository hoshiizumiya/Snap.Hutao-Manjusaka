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
using System.Net.Http;

namespace Snap.Hutao.Service.ThirdPartyTool;

[HttpClient(HttpClientConfiguration.Default)]
[Service(ServiceLifetime.Singleton, typeof(IThirdPartyToolService))]
internal sealed partial class ThirdPartyToolService : IThirdPartyToolService
{
    private const string ApiBaseUrl = "https://htserver.wdg.cloudns.ch/api";
    private const string ToolsEndpoint = "/tools";

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
            Directory.CreateDirectory(toolDirectory);

            int totalFiles = tool.Files.Count;
            int downloadedFiles = 0;

            using (HttpClient httpClient = httpClientFactory.CreateClient())
            {
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

            // 查找可执行文件（.exe）
            string? executablePath = tool.Files.FirstOrDefault(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(executablePath))
            {
                messenger.Send(InfoBarMessage.Warning(SH.ServiceThirdPartyToolNoExecutableFound));
                return false;
            }

            string fullPath = Path.Combine(toolDirectory, executablePath);
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

        // 检查所有文件是否存在
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

    private static string GetToolDirectory(ToolInfo tool)
    {
        // 使用数据目录/工具名作为存储路径
        return Path.Combine(HutaoRuntime.DataDirectory, "Tools", tool.Name);
    }
}
