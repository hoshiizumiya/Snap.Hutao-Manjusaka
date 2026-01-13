// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core;
using Snap.Hutao.Core.Diagnostics;
using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.Setting;
using Snap.Hutao.Service.Game.FileSystem;
using Snap.Hutao.Service.Game.Launching.Context;
using Snap.Hutao.Web.Hutao;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Snap.Hutao.Service.Game.Island;

internal sealed class GameFpsUnlockInterop : IGameIslandInterop, IDisposable
{
    private const string UnlockerExecutableName = "unlockfps.exe";
    private const string UnlockerConfigName = "fps_config.ini";

    private readonly bool resume;

    private string? unlockerPath;
    private string? gamePath;
    private Process? unlockerProcess;

    public GameFpsUnlockInterop(bool resume)
    {
        this.resume = resume;
    }

    public async ValueTask BeforeAsync(BeforeLaunchExecutionContext context)
    {
        if (resume)
        {
            return;
        }

        // 获取unlocker.exe路径，放在Snap.Hutao同一目录下
        string hutaoDirectory = AppContext.BaseDirectory;
        unlockerPath = Path.Combine(hutaoDirectory, UnlockerExecutableName);
        
        if (!File.Exists(unlockerPath))
        {
            throw HutaoException.InvalidOperation("未找到unlockfps.exe文件，请将genshin-fps-unlock-master编译后的unlockfps.exe放置在Snap.Hutao同目录下");
        }

        // 获取游戏路径
        gamePath = context.FileSystem.GameFilePath;
        
        // 验证游戏路径
        SentrySdk.AddBreadcrumb(
            $"Game path from Snap.Hutao: {gamePath}",
            category: "fps.unlocker",
            level: Sentry.BreadcrumbLevel.Info);
            
            
        if (!File.Exists(gamePath))
        {
            throw HutaoException.InvalidOperation($"游戏文件不存在: {gamePath}");
        }

        // 创建配置文件
        await CreateUnlockerConfigAsync(context).ConfigureAwait(false);

        // 启动解锁器进程
        await StartUnlockerProcessAsync(context, CancellationToken.None).ConfigureAwait(false);
    }

    public async ValueTask WaitForExitAsync(LaunchExecutionContext context, CancellationToken token = default)
    {
        if (resume)
        {
            // 恢复模式下，尝试连接已存在的解锁器进程
            await MonitorExistingUnlockerAsync(context, token).ConfigureAwait(false);
            return;
        }

        // 监控解锁器进程状态（解锁器会自动启动并监控游戏）
        await MonitorUnlockerProcessAsync(context, token).ConfigureAwait(false);
    }

    private async ValueTask CreateUnlockerConfigAsync(BeforeLaunchExecutionContext context)
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            throw HutaoException.NotSupported("游戏路径未初始化");
        }

        // 直接在unlocker同目录创建配置文件
        string unlockerConfigPath = Path.Combine(Path.GetDirectoryName(unlockerPath)!, UnlockerConfigName);
        int targetFps = context.LaunchOptions.TargetFps.Value;
        
        string configContent = $"[Setting]\nPath={gamePath}\nFPS={targetFps}";
        
        // 添加重试机制处理可能的权限问题
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await File.WriteAllTextAsync(unlockerConfigPath, configContent).ConfigureAwait(false);
                break; // 成功写入，退出循环
            }
            catch (UnauthorizedAccessException)
            {
                if (i == 2)
                {
                    throw HutaoException.InvalidOperation($"无法写入配置文件 {unlockerConfigPath}，请检查权限");
                }
                await Task.Delay(500).ConfigureAwait(false);
            }
            catch (IOException)
            {
                if (i == 2)
                {
                    throw HutaoException.InvalidOperation($"无法写入配置文件 {unlockerConfigPath}，文件可能被占用");
                }
                await Task.Delay(500).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask StartUnlockerProcessAsync(BeforeLaunchExecutionContext context, CancellationToken token)
    {
        try
        {

            string configPath = Path.Combine(Path.GetDirectoryName(unlockerPath)!, UnlockerConfigName);
            if (!File.Exists(configPath))
            {
                throw HutaoException.InvalidOperation($"配置文件不存在: {configPath}");
            }


            string configContent = await File.ReadAllTextAsync(configPath).ConfigureAwait(false);
            SentrySdk.AddBreadcrumb(
                $"Starting unlocker with config: {configContent}",
                category: "fps.unlocker",
                level: Sentry.BreadcrumbLevel.Info);

            // 构建游戏启动参数，传递给 unlockfps.exe
            string gameArguments = BuildGameArguments(context);
            SentrySdk.AddBreadcrumb(
                $"Game arguments for unlocker: {gameArguments}",
                category: "fps.unlocker",
                level: Sentry.BreadcrumbLevel.Info);

            ProcessStartInfo startInfo = new()
            {
                FileName = unlockerPath,
                WorkingDirectory = Path.GetDirectoryName(unlockerPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Normal,
                Arguments = gameArguments,
            };

            unlockerProcess = new Process { StartInfo = startInfo };


            unlockerProcess.Start();


            Task outputTask = Task.Run(async () =>
            {
                while (!unlockerProcess.StandardOutput.EndOfStream)
                {
                    string line = await unlockerProcess.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (line != null)
                    {
                        SentrySdk.AddBreadcrumb(
                            $"Unlocker output: {line}",
                            category: "fps.unlocker",
                            level: Sentry.BreadcrumbLevel.Info);
                    }
                }
            });

            Task errorTask = Task.Run(async () =>
            {
                while (!unlockerProcess.StandardError.EndOfStream)
                {
                    string line = await unlockerProcess.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (line != null)
                    {
                        SentrySdk.AddBreadcrumb(
                            $"Unlocker error: {line}",
                            category: "fps.unlocker",
                            level: Sentry.BreadcrumbLevel.Error);
                    }
                }
            });

            // 等待解锁器初始化
            await Task.Delay(5000).ConfigureAwait(false);


        }
        catch (Exception ex)
        {
            throw HutaoException.Throw($"启动FPS解锁器失败: {ex.Message}", ex);
        }
    }

    private string BuildGameArguments(BeforeLaunchExecutionContext context)
    {
        LaunchOptions launchOptions = context.LaunchOptions;

        if (!launchOptions.AreCommandLineArgumentsEnabled.Value)
        {
            return string.Empty;
        }

        StringBuilder arguments = new();

        // 构建与 GameProcessFactory.CreateForDefault 相同的命令行参数
        if (launchOptions.IsBorderless.Value)
        {
            arguments.Append(" -popupwindow");
        }

        if (launchOptions.IsExclusive.Value)
        {
            arguments.Append(" -window-mode exclusive");
        }

        arguments.Append($" -screen-fullscreen {(launchOptions.IsFullScreen.Value ? "1" : "0")}");

        if (launchOptions.IsScreenWidthEnabled.Value)
        {
            arguments.Append($" -screen-width {launchOptions.ScreenWidth.Value}");
        }

        if (launchOptions.IsScreenHeightEnabled.Value)
        {
            arguments.Append($" -screen-height {launchOptions.ScreenHeight.Value}");
        }

        if (launchOptions.IsMonitorEnabled.Value)
        {
            arguments.Append($" -monitor {launchOptions.Monitor.Value?.Value ?? 1}");
        }

        if (launchOptions.IsPlatformTypeEnabled.Value)
        {
            arguments.Append($" -platform_type {launchOptions.PlatformType.Value:G}");
        }

        return arguments.ToString();
    }

    private async ValueTask MonitorExistingUnlockerAsync(LaunchExecutionContext context, CancellationToken token)
    {
        // 恢复模式下，检查是否有解锁器进程在运行
        Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(UnlockerExecutableName));
        if (processes.Length == 0)
        {
            // 没有找到解锁器进程，但游戏在运行，这是正常情况
            return;
        }

        unlockerProcess = processes[0];
        await MonitorUnlockerProcessAsync(context, token).ConfigureAwait(false);
    }

    private async ValueTask MonitorUnlockerProcessAsync(LaunchExecutionContext context, CancellationToken token)
    {
        if (unlockerProcess is null)
        {
            return;
        }

        using (PeriodicTimer timer = new(TimeSpan.FromSeconds(2)))
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                // 检查解锁器进程状态
                if (unlockerProcess.HasExited)
                {
                    // 解锁器已退出，这意味着游戏也已退出
                    break;
                }

                // 同步FPS设置（如果用户在运行时修改了）
                await SyncFpsSettingsAsync(context.LaunchOptions).ConfigureAwait(false);
            }
        }

        // 确保解锁器进程已清理
        CleanupUnlockerProcess();
    }

    private async ValueTask SyncFpsSettingsAsync(LaunchOptions launchOptions)
    {
        if (unlockerProcess is null || unlockerProcess.HasExited)
        {
            return;
        }

        try
        {
            string configPath = Path.Combine(Path.GetDirectoryName(unlockerPath)!, UnlockerConfigName);
            if (File.Exists(configPath))
            {
                string[] lines = await File.ReadAllLinesAsync(configPath).ConfigureAwait(false);
                int currentFps = launchOptions.TargetFps.Value;
                
                bool needsUpdate = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("FPS="))
                    {
                        int configFps = int.Parse(lines[i].Substring(4));
                        if (configFps != currentFps)
                        {
                            lines[i] = $"FPS={currentFps}";
                            needsUpdate = true;
                        }
                        break;
                    }
                }

                if (needsUpdate)
                {
                    // 添加重试机制处理可能的权限问题
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            await File.WriteAllLinesAsync(configPath, lines).ConfigureAwait(false);
                            break; // 成功写入，退出循环
                        }
                        catch (UnauthorizedAccessException)
                        {
                            if (i == 2) // 最后一次尝试
                            {
                                SentrySdk.AddBreadcrumb(
                                    $"无法写入配置文件 {configPath}，请检查权限",
                                    category: "fps.unlocker",
                                    level: Sentry.BreadcrumbLevel.Error);
                                return;
                            }
                            await Task.Delay(500).ConfigureAwait(false); // 等待500ms后重试
                        }
                        catch (IOException)
                        {
                            if (i == 2) // 最后一次尝试
                            {
                                SentrySdk.AddBreadcrumb(
                                    $"无法写入配置文件 {configPath}，文件可能被占用",
                                    category: "fps.unlocker",
                                    level: Sentry.BreadcrumbLevel.Error);
                                return;
                            }
                            await Task.Delay(500).ConfigureAwait(false); // 等待500ms后重试
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 同步配置失败，记录但不影响主流程
            SentrySdk.AddBreadcrumb(
                $"Failed to sync FPS settings: {ex.Message}",
                category: "fps.unlocker",
                level: Sentry.BreadcrumbLevel.Warning);
        }
    }

    

    private void CleanupUnlockerProcess()
    {
        if (unlockerProcess is not null && !unlockerProcess.HasExited)
        {
            try
            {
                unlockerProcess.Kill();
                unlockerProcess.WaitForExit();
            }
            catch (Exception ex)
            {
                // 忽略清理过程中的错误
                SentrySdk.AddBreadcrumb(
                    $"Failed to cleanup unlocker process: {ex.Message}",
                    category: "fps.unlocker",
                    level: Sentry.BreadcrumbLevel.Warning);
            }
            finally
            {
                unlockerProcess.Dispose();
                unlockerProcess = null;
            }
        }
    }

    public void Dispose()
    {
        CleanupUnlockerProcess();
    }
}