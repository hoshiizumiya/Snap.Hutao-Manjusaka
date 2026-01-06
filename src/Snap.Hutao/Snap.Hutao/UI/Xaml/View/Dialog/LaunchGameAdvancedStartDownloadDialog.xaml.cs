// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Common;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using Snap.Hutao.Core;
using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.IO;
using Snap.Hutao.Core.Setting;
using Snap.Hutao.Factory.ContentDialog;
using Snap.Hutao.Factory.Progress;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Web.Request.Builder;
using Snap.Hutao.Web.Request.Builder.Abstraction;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace Snap.Hutao.UI.Xaml.View.Dialog;

[DependencyProperty<ObservableCollection<AdvancedStartProgramOption>>("Programs")]
[DependencyProperty<AdvancedStartProgramOption>("SelectedProgram", PropertyChangedCallbackName = nameof(OnSelectedProgramChanged))]
[DependencyProperty<double>("ProgressValue", DefaultValue = 0d, NotNull = true)]
[DependencyProperty<string>("StatusText")]
[DependencyProperty<bool>("IsBusy", DefaultValue = false, NotNull = true, PropertyChangedCallbackName = nameof(OnIsBusyChanged))]
[DependencyProperty<bool>("IsLoading", DefaultValue = false, NotNull = true, PropertyChangedCallbackName = nameof(OnIsLoadingChanged))]
[DependencyProperty<string>("ErrorText")]
[DependencyProperty<bool>("HasError", DefaultValue = false, NotNull = true)]
[DependencyProperty<bool>("CanDownload", DefaultValue = false, NotNull = true)]
[DependencyProperty<bool>("IsListEnabled", DefaultValue = true, NotNull = true)]
internal sealed partial class LaunchGameAdvancedStartDownloadDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly HttpClient httpClient;
    private IProgress<StreamCopyStatus>? downloadProgress;

    private string? downloadedPath;
    private string currentProgramName = string.Empty;

    [GeneratedConstructor(InitializeComponent = true)]
    public partial LaunchGameAdvancedStartDownloadDialog(IServiceProvider serviceProvider);

    partial void PostConstruct(IServiceProvider serviceProvider)
    {
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(HutaoRuntime.UserAgent);
        downloadProgress = serviceProvider.GetRequiredService<IProgressFactory>().CreateForMainThread<StreamCopyStatus>(UpdateDownloadProgress);
        Programs = [];
    }

    public async ValueTask<ValueResult<bool, string?>> GetResultAsync()
    {
        ContentDialogResult result = await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false);
        return new(result is ContentDialogResult.Primary && downloadedPath is not null, downloadedPath);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _ = LoadProgramsAsync();
    }

    private async Task LoadProgramsAsync()
    {
        await taskContext.SwitchToMainThreadAsync();
        IsLoading = true;
        HasError = false;
        ErrorText = string.Empty;
        StatusText = SH.ViewDialogLaunchGameAdvancedStartLoading;
        UpdateCanDownload();

        string feedEndpoint = LocalSetting.Get(SettingKeys.LaunchAdvancedStartFeedEndpoint, "https://hutaofeed.pages.dev/programs.json");
        if (string.IsNullOrWhiteSpace(feedEndpoint))
        {
            HasError = true;
            ErrorText = SH.ViewDialogLaunchGameAdvancedStartFeedMissing;
            IsLoading = false;
            UpdateCanDownload();
            return;
        }

        try
        {
            await taskContext.SwitchToBackgroundAsync();
            ObservableCollection<AdvancedStartProgramOption> programs = await FetchProgramsAsync(feedEndpoint).ConfigureAwait(false);

            await taskContext.SwitchToMainThreadAsync();
            Programs = programs;
            StatusText = SH.ViewDialogLaunchGameAdvancedStartPick;
        }
        catch (Exception ex)
        {
            await taskContext.SwitchToMainThreadAsync();
            HasError = true;
            ErrorText = SH.ViewDialogLaunchGameAdvancedStartLoadFailed;
            messenger.Send(InfoBarMessage.Error(ex));
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsLoading = false;
            UpdateCanDownload();
        }
    }

    private async Task<ObservableCollection<AdvancedStartProgramOption>> FetchProgramsAsync(string endpoint)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create().SetRequestUri(endpoint).Get();
        using HttpRequestMessage message = builder.HttpRequestMessage;
        using HttpResponseMessage response = await httpClient.SendAsync(message).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        if (await JsonSerializer.DeserializeAsync<AdvancedStartProgramFeed>(stream, jsonOptions).ConfigureAwait(false) is AdvancedStartProgramFeed { Programs: { Count: > 0 } programs })
        {
            return new(programs);
        }

        stream.Seek(0, SeekOrigin.Begin);
        if (await JsonSerializer.DeserializeAsync<AdvancedStartProgramFeed>(stream, jsonOptions).ConfigureAwait(false) is AdvancedStartProgramFeed { Items: { Count: > 0 } items })
        {
            return new(items);
        }

        stream.Seek(0, SeekOrigin.Begin);
        if (await JsonSerializer.DeserializeAsync<List<AdvancedStartProgramOption>>(stream, jsonOptions).ConfigureAwait(false) is { Count: > 0 } list)
        {
            return new(list);
        }

        return [];
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();

        bool success = false;
        try
        {
            success = await DownloadAndExtractAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Ensure reporting runs on UI thread
            await taskContext.SwitchToMainThreadAsync();
            messenger.Send(InfoBarMessage.Error(ex));
            success = false;
        }

        // Must switch to UI thread before touching XAML/COM objects
        await taskContext.SwitchToMainThreadAsync();
        args.Cancel = !success;

        deferral.Complete();
    }

    private async Task<bool> DownloadAndExtractAsync()
    {
        AdvancedStartProgramOption program;

        // ① UI 线程：只读一次 DependencyProperty
        await taskContext.SwitchToMainThreadAsync();

        if (SelectedProgram is null)
        {
            messenger.Send(InfoBarMessage.Warning(
                SH.ViewDialogLaunchGameAdvancedStartPickWarning));
            return false;
        }

        program = SelectedProgram;              // ← 快照
        currentProgramName = program.Name;

        HasError = false;
        ErrorText = string.Empty;
        IsBusy = true;
        ProgressValue = 0;
        UpdateCanDownload();

        try
        {
            // ② 后台线程：只使用普通对象
            await taskContext.SwitchToBackgroundAsync();
            string programPath = await DownloadSelectedProgramAsync(program);

            // ③ UI 线程：更新 UI
            await taskContext.SwitchToMainThreadAsync();
            downloadedPath = programPath;
            StatusText = SH.FormatViewDialogLaunchGameAdvancedStartCompleted(programPath);
            ProgressValue = 1;
            return true;
        }
        catch (Exception ex)
        {
            await taskContext.SwitchToMainThreadAsync();
            HasError = true;
            ErrorText = SH.ViewDialogLaunchGameAdvancedStartFailed;
            messenger.Send(InfoBarMessage.Error(ex));
            return false;
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsBusy = false;
            UpdateCanDownload();
        }
    }


    private async ValueTask<string> DownloadSelectedProgramAsync(AdvancedStartProgramOption program)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create().SetRequestUri(program.Url).Get();

        using HttpRequestMessage message = builder.HttpRequestMessage;
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketEx)
        {
            // 特别处理SocketException（DNS、连接失败等）
            switch (socketEx.SocketErrorCode)
            {
                case SocketError.HostNotFound:  // 11001 - DNS解析失败
                                                // 专门处理DNS解析失败，提供更详细的用户指导
                    string hostName = message.RequestUri?.Host ?? SH.ViewDialogLaunchGameAdvancedStartUnknownServer;
                    throw HutaoException.InvalidOperation(
                        string.Format(SH.ViewDialogLaunchGameAdvancedStartDnsFailed, hostName));

                case SocketError.ConnectionRefused:  // 10061 - 连接被拒绝
                    throw HutaoException.InvalidOperation(SH.ViewDialogLaunchGameAdvancedStartConnectionRefused);

                case SocketError.TimedOut:  // 10060 - 连接超时
                    throw HutaoException.InvalidOperation(SH.ViewDialogLaunchGameAdvancedStartTimedOut);

                case SocketError.NetworkDown:  // 10050 - 网络不可用
                    throw HutaoException.InvalidOperation(SH.ViewDialogLaunchGameAdvancedStartNetworkDown);

                case SocketError.NetworkUnreachable:  // 10051 - 网络不可达
                    throw HutaoException.InvalidOperation(SH.ViewDialogLaunchGameAdvancedStartNetworkUnreachable);

                case SocketError.NoData:  // 10054 - DNS查询成功但无记录
                    throw HutaoException.InvalidOperation(SH.ViewDialogLaunchGameAdvancedStartDnsNoData);

                default:
                    // 记录原始异常信息以便调试
                    throw HutaoException.InvalidOperation(string.Format(SH.ViewDialogLaunchGameAdvancedStartNetworkError, socketEx.Message, socketEx.SocketErrorCode));
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            // 处理有HTTP状态码的异常（如4xx/5xx响应，但仍然抛出异常的情况）
            // 注意：HttpRequestException.StatusCode在.NET 5+中才可用
            if (ex.StatusCode is HttpStatusCode statusCode)
            {
                string statusCodeDescription = statusCode.ToString();

                throw HutaoException.InvalidOperation(string.Format(SH.ViewDialogLaunchGameAdvancedStartHttpError, (int)statusCode, statusCodeDescription));
            }
            else
            {
                throw HutaoException.InvalidOperation(string.Format(SH.ViewDialogLaunchGameAdvancedStartUnknownServerResponse, ex.Message));
            }
        }
        catch (HttpRequestException ex)
        {
            // 其他HttpRequestException（如SSL/TLS错误等）
            // 检查是否是证书相关错误
            if (ex.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("TLS", StringComparison.OrdinalIgnoreCase))
            {
                throw HutaoException.InvalidOperation(string.Format(SH.ViewDialogLaunchGameAdvancedStartSecurityFailed, ex.Message));
            }

            // 通用网络请求异常
            throw HutaoException.InvalidOperation(string.Format(SH.ViewDialogLaunchGameAdvancedStartNetworkRequestFailed, ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            // 区分超时和用户取消
            // TaskCanceledException可能由以下原因引起：
            // 1. 用户取消操作 (CancellationToken.IsCancellationRequested == true)
            // 2. HttpClient超时 (HttpClient.Timeout)
            // 3. 网络超时

            if (ex.CancellationToken.IsCancellationRequested)
            {
                // 用户主动取消
                throw HutaoException.OperationCanceled(string.Format(SH.ViewDialogLaunchGameAdvancedStartDownloadCanceled, program.Name));
            }
            else
            {
                // 通常是由HttpClient.Timeout引起的超时
                // 但也可能是网络层的超时
                throw HutaoException.InvalidOperation(string.Format(SH.ViewDialogLaunchGameAdvancedStartDownloadTimeout, program.Name));
            }
        }
        catch (OperationCanceledException ex)
        {
            // 处理更通用的取消异常（来自CancellationTokenSource）
            throw HutaoException.OperationCanceled(string.Format(SH.ViewDialogLaunchGameAdvancedStartOperationCanceled, ex.Message));
        }
        catch (IOException ex)
        {
            // 处理IO相关的异常（如写入文件时）
            throw HutaoException.IO(string.Format(SH.ViewDialogLaunchGameAdvancedStartIOError, ex.Message));
        }
        catch (NotSupportedException ex) when (ex.Message.Contains("Content", StringComparison.OrdinalIgnoreCase))
        {
            // 处理不支持的内容类型
            throw HutaoException.InvalidOperation(string.Format(SH.ViewDialogLaunchGameAdvancedStartUnsupportedContentType, ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("headers", StringComparison.OrdinalIgnoreCase))
        {
            // 处理无效的HTTP头
            throw HutaoException.InvalidOperation(string.Format(SH.ViewDialogLaunchGameAdvancedStartInvalidHeaders, ex.Message));
        }
        catch (Exception ex)
        {
            // 其他未预期的异常
            // 提供用户友好的错误信息，同时保留技术细节供技术支持
            throw HutaoException.InvalidOperation(
                string.Format(SH.ViewDialogLaunchGameAdvancedStartUnknownError, program.Name, ex.GetType().Name));
        }
        finally
        {
            // 清理资源
            // 注意：HttpRequestMessage通常由调用方释放，但在这里作为局部变量，应该释放
            // 如果response被正常返回，调用方负责释放response
            message?.Dispose();
        }
        using (response)
        {
            response.EnsureSuccessStatusCode();

            long contentLength = response.Content.Headers.ContentLength ?? 0;
            await using Stream content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using TempFileStream tempStream = new(FileMode.OpenOrCreate, FileAccess.ReadWrite);

            using (StreamCopyWorker worker = new(content, tempStream, contentLength))
            {
                await worker.CopyAsync(downloadProgress).ConfigureAwait(false);
            }

            tempStream.Seek(0, SeekOrigin.Begin);

            string baseFolder = Path.Combine(HutaoRuntime.DataDirectory, "AdvancedStart");
            Directory.CreateDirectory(baseFolder);

            string targetFolder = Path.Combine(baseFolder, SanitizePathSegment(program.Name), SanitizePathSegment(string.IsNullOrWhiteSpace(program.Version) ? "latest" : program.Version));

            if (Directory.Exists(targetFolder))
            {
                Directory.Delete(targetFolder, true);
            }

            Directory.CreateDirectory(targetFolder);
            await ExtractArchiveAsync(tempStream, targetFolder).ConfigureAwait(false);

            string? executablePath = ResolveExecutablePath(targetFolder, program.Entry);
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw HutaoException.InvalidOperation(SH.ViewDialogLaunchGameAdvancedStartExecutableMissing);
            }

            return executablePath;
        }
    }

    private async ValueTask ExtractArchiveAsync(Stream stream, string targetFolder)
    {
        // Detect archive type by signature
        byte[] header = new byte[6];
        int read = await stream.ReadAsync(header.AsMemory(0, header.Length)).ConfigureAwait(false);
        stream.Seek(0, SeekOrigin.Begin);

        bool isZip = read >= 2 && header[0] == 0x50 && header[1] == 0x4B; // 'PK'
        bool is7z = read >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC && header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C; // 7z signature

        if (isZip)
        {
            using ZipArchive archive = await ZipArchive.CreateAsync(stream, ZipArchiveMode.Read, false, default);
            int totalEntries = archive.Entries.Count;
            int current = 0;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.Combine(targetFolder, entry.FullName);
                string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    current++;
                    continue;
                }

                await using FileStream fileStream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await using Stream entryStream = entry.Open();
                await entryStream.CopyToAsync(fileStream).ConfigureAwait(false);

                current++;
                await taskContext.SwitchToMainThreadAsync();
                ProgressValue = totalEntries is 0 ? 0 : (double)current / totalEntries;
                StatusText = SH.FormatViewDialogLaunchGameAdvancedStartExtracting(currentProgramName, current, totalEntries);
            }

            return;
        }

        if (is7z)
        {
            // SharpCompress used to extract 7z archives.
            // Ensure the project references SharpCompress NuGet package.
            string? tempFilePath = null;
            Stream sevenZipStream = stream;
            try
            {
                // Ensure we provide a seekable stream to SharpCompress.
                if (!stream.CanSeek)
                {
                    // Copy to a temp file and reopen for reading.
                    tempFilePath = Path.GetTempFileName();
                    await using (FileStream fsTempWrite = File.Open(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        await stream.CopyToAsync(fsTempWrite).ConfigureAwait(false);
                        await fsTempWrite.FlushAsync().ConfigureAwait(false);
                    }

                    sevenZipStream = File.OpenRead(tempFilePath);
                }
                else
                {
                    sevenZipStream.Seek(0, SeekOrigin.Begin);
                }

                using SevenZipArchive archive = SevenZipArchive.Open(sevenZipStream);
                List<SevenZipArchiveEntry> entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                int totalEntries = entries.Count;
                int current = 0;

                foreach (SevenZipArchiveEntry entry in entries)
                {
                    string entryPath = entry.Key.Replace('/', Path.DirectorySeparatorChar);
                    string destinationPath = Path.Combine(targetFolder, entryPath);
                    string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }

                    try
                    {
                        await using Stream entryStream = entry.OpenEntryStream();
                        await using FileStream fileStream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await entryStream.CopyToAsync(fileStream).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (ex.GetType().Name == "DataErrorException" || ex.InnerException?.GetType().Name == "DataErrorException")
                        {
                            throw new InvalidDataException(string.Format(SH.ViewDialogLaunchGameAdvancedStart7zEntryDataError, entry.Key), ex);
                        }

                        throw;
                    }

                    current++;
                    await taskContext.SwitchToMainThreadAsync();
                    ProgressValue = totalEntries is 0 ? 0 : (double)current / totalEntries;
                    StatusText = SH.FormatViewDialogLaunchGameAdvancedStartExtracting(currentProgramName, current, totalEntries);
                }

                return;
            }
            catch (Exception ex)
            {
                // If underlying exception is the inaccessible LZMA DataErrorException, wrap and surface as InvalidDataException
                if (ex.GetType().Name == "DataErrorException" || ex.InnerException?.GetType().Name == "DataErrorException")
                {
                    throw new InvalidDataException(SH.ViewDialogLaunchGameAdvancedStart7zDataError, ex);
                }

                throw;
            }
            finally
            {
                try
                {
                    if (!ReferenceEquals(sevenZipStream, stream))
                    {
                        sevenZipStream.Dispose();
                    }
                }
                catch { }

                if (tempFilePath is not null)
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch { }
                }
            }
        }

        // Unsupported archive format
        throw new InvalidDataException(SH.ViewDialogLaunchGameAdvancedStartUnsupportedArchiveFormat);
    }

    private string? ResolveExecutablePath(string targetFolder, string? entryPath)
    {
        if (!string.IsNullOrWhiteSpace(entryPath))
        {
            string candidate = Path.Combine(targetFolder, entryPath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string[] executables = Directory.GetFiles(targetFolder, "*.exe", SearchOption.AllDirectories);
        return executables.FirstOrDefault();
    }

    private void UpdateDownloadProgress(StreamCopyStatus status)
    {
        StatusText = SH.FormatViewDialogLaunchGameAdvancedStartDownloading(currentProgramName, Converters.ToFileSizeString(status.BytesReadSinceCopyStart), Converters.ToFileSizeString(status.TotalBytes));
        ProgressValue = status.TotalBytes is 0 ? 0 : (double)status.BytesReadSinceCopyStart / status.TotalBytes;
    }

    private static void OnSelectedProgramChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((LaunchGameAdvancedStartDownloadDialog)sender).UpdateCanDownload();
    }

    private static void OnIsBusyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((LaunchGameAdvancedStartDownloadDialog)sender).UpdateCanDownload();
    }

    private static void OnIsLoadingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        ((LaunchGameAdvancedStartDownloadDialog)sender).UpdateCanDownload();
    }

    private void UpdateCanDownload()
    {
        IsListEnabled = !IsBusy && !IsLoading;
        CanDownload = SelectedProgram is not null && IsListEnabled && !HasError;
        IsPrimaryButtonEnabled = CanDownload;
    }

    private static string SanitizePathSegment(string segment)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            segment = segment.Replace(invalid, '_');
        }

        return segment;
    }

    private sealed class AdvancedStartProgramFeed
    {
        public List<AdvancedStartProgramOption> Programs { get; set; } = [];

        public List<AdvancedStartProgramOption>? Items { get; set; }
    }
}

internal sealed class AdvancedStartProgramOption
{
    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? Entry { get; set; }

    public string? Description { get; set; }
}
