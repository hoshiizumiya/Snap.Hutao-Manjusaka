// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Snap.Hutao.Core;
using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.Setting;
using Snap.Hutao.Service.Notification;

namespace Snap.Hutao.Service;

[Service(ServiceLifetime.Singleton)]
internal sealed partial class AutoStartService
{
    private const string TaskName = "SnapHutao AutoStart";

    public AutoStartService(IServiceProvider serviceProvider)
    {
    }

    public bool IsStartupEnabled()
    {
        try
        {
            return LocalSetting.Get(SettingKeys.StartupEnabled, false);
        }
        catch
        {
            return false;
        }
    }

    public bool IsRunElevatedEnabled()
    {
        try
        {
            return LocalSetting.Get(SettingKeys.RunElevated, false);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 确保自启动任务与当前设置一致（是否启用/是否最高权限）。
    /// 用于应用启动时、以及在管理员模式下切换“始终以管理员身份运行”时的同步。
    /// </summary>
    public void EnsureUpToDate()
    {
        try
        {
            bool enable = IsStartupEnabled();
            bool expectedRunElevated = IsRunElevatedEnabled();

            if (!enable)
            {
                return;
            }

            // Runner.dll 不可用时直接返回，避免在启动阶段弹错或阻塞
            if (!TryUseNativeHelper(out _))
            {
                return;
            }

            bool taskActive;
            try
            {
                taskActive = NativeMethods.is_auto_start_task_active_for_this_user();
            }
            catch
            {
                taskActive = false;
            }

            bool taskExecutableValid;
            bool taskExecutableMatchesCurrent;
            try
            {
                taskExecutableValid = AutoStartService.TryGetAutoStartTaskExecutablePath(out string? taskExePath)
                    && !string.IsNullOrWhiteSpace(taskExePath)
                    && File.Exists(taskExePath);

                taskExecutableMatchesCurrent = taskExecutableValid && IsExecutablePathMatchCurrent(taskExePath!);
            }
            catch
            {
                taskExecutableValid = false;
                taskExecutableMatchesCurrent = false;
            }

            if (!taskActive)
            {
                // 非管理员进程不尝试修复（任务创建/删除需要权限），仅保留现状
                if (!HutaoRuntime.IsProcessElevated)
                {
                    return;
                }

                SetStartup(true, expectedRunElevated);
                return;
            }

            bool taskElevatedMatches;
            try
            {
                bool taskRunElevated = NativeMethods.is_auto_start_task_run_elevated_for_this_user();
                taskElevatedMatches = taskRunElevated == expectedRunElevated;
            }
            catch
            {
                taskElevatedMatches = false;
            }

            // 只要任务 exe 不存在 / 路径不等于当前 exe / 权限不一致，都视为不一致并修复
            if (!taskExecutableValid || !taskExecutableMatchesCurrent || !taskElevatedMatches)
            {
                if (!HutaoRuntime.IsProcessElevated)
                {
                    return;
                }

                SetStartup(true, expectedRunElevated);
            }
        }
        catch
        {
        }
    }

    private static bool IsExecutablePathMatchCurrent(string taskExePath)
    {
        string currentPath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrEmpty(currentPath) || string.IsNullOrEmpty(taskExePath))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(taskExePath), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(taskExePath, currentPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool TryGetAutoStartTaskExecutablePath(out string? taskPath)
    {
        taskPath = null;

        char[] buffer = ArrayPool<char>.Shared.Rent(1024);
        try
        {
            if (!NativeMethods.TryGetAutoStartTaskExecutablePath(buffer))
            {
                return false;
            }

            int idx = Array.IndexOf(buffer, '\0');
            if (idx < 0)
            {
                idx = buffer.Length;
            }

            taskPath = new(buffer, 0, idx);
            return true;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    // 兼容旧调用点（内部已收敛到 EnsureUpToDate）
    public void EnsureValidityAsync()
    {
        EnsureUpToDate();
    }

    // 兼容旧调用点（内部已收敛到 EnsureUpToDate）
    public void SyncAutoStartPrivilegeSettings(bool expectedRunElevated)
    {
        try
        {
            if (!IsStartupEnabled())
            {
                return;
            }

            // 如果调用者传入了期望值，但本地设置可能已变化；优先以本地设置为准
            _ = expectedRunElevated;
            EnsureUpToDate();
        }
        catch
        {
        }
    }

    public void SetStartup(bool enable, bool runElevated)
    {
        try
        {
            if (enable)
            {
                RegisterAutoStartTask(runElevated);
            }
            else
            {
                UnregisterAutoStartTask();
            }
            LocalSetting.Set(SettingKeys.StartupEnabled, enable);
            LocalSetting.Set(SettingKeys.RunElevated, runElevated);
        }
        catch (Exception ex)
        {
            try { SentrySdk.CaptureException(ex); } catch { }
            throw;
        }
    }

    /// <summary>
    /// 兼容旧调用点：路径/权限一致性检查由 <see cref="EnsureUpToDate"/> 统一负责。
    /// </summary>
    public bool IsExecutablePathValid()
    {
        try
        {
            if (!IsStartupEnabled())
            {
                return true;
            }

            // With native helper available, task presence is the minimal signal.
            if (!TryUseNativeHelper(out _))
            {
                return true;
            }

            return NativeMethods.is_auto_start_task_active_for_this_user();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryUseNativeHelper(out string? reason)
    {
        reason = null;
        try
        {
            // Try load the runner dll from the same folder as the executable
            string exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty) ?? string.Empty;
            string helperPath = Path.Combine(exeDir, "Runner.dll");
            if (!File.Exists(helperPath))
            {
                reason = "Runner.dll not found.";
                IMessenger messenger = Ioc.Default.GetRequiredService<IMessenger>();
                messenger.Send(InfoBarMessage.Error("AutoStart feature is unavailable because Runner.dll is missing."));
                return false;
            }

            // Attempt to load library
            IntPtr h = NativeMethods.LoadLibrary(helperPath);
            if (h == IntPtr.Zero)
            {
                reason = $"LoadLibrary failed: {Marshal.GetLastWin32Error()}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    private void RegisterAutoStartTask(bool runElevated)
    {
        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            throw HutaoException.InvalidOperation("Cannot find executable path to register autostart task.");
        }

        // Prefer native helper that uses Task Scheduler COM to set trigger UserId
        if (TryUseNativeHelper(out _))
        {
            try
            {
                // Runner 的实现：若任务已存在会仅 put_Enabled(true) 并直接返回，
                // 不会更新 Path/RunLevel 等定义。这里先尝试删除旧任务，确保后续注册能生效。
                // 失败则继续尝试创建（例如任务不存在或无权限）。
                _ = NativeMethods.delete_auto_start_task_for_this_user();

                bool ok = NativeMethods.create_auto_start_task_for_this_user(runElevated ? 1 : 0);
                if (ok)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                try { SentrySdk.CaptureException(ex); } catch { }
            }
        }
    }

    private void UnregisterAutoStartTask()
    {
        // Prefer native helper
        if (TryUseNativeHelper(out _))
        {
            try
            {
                // For 1.17.3.x version wrongly set the task without current user trigger
                bool ok = NativeMethods.delete_auto_start_task_for_this_user();
                if (ok)
                {
                    ProcessStartInfo psi = new()
                    {
                        FileName = "schtasks.exe",
                        Arguments = $"/Delete /TN \"{TaskName}\" /F",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                    };

                    using Process proc = Process.Start(psi)!;
                    proc.WaitForExit();

                    return;
                }
            }
            catch
            {
            }
        }
    }


    static partial class NativeMethods
    {
        private const string KERNEL32 = "kernel32.dll";

        // 使用 NativeLibrary.Load/Free 代替直接 P/Invoke LoadLibrary/FreeLibrary，
        // 避免 EntryPoint 名称差异（LoadLibraryW/LoadLibraryA）引起的问题。
        public static IntPtr LoadLibrary(string lpFileName)
        {
            try
            {
                return System.Runtime.InteropServices.NativeLibrary.Load(lpFileName);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        public static bool FreeLibrary(IntPtr hModule)
        {
            if (hModule == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                System.Runtime.InteropServices.NativeLibrary.Free(hModule);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [LibraryImport("Runner.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool create_auto_start_task_for_this_user(int runElevated);

        [LibraryImport("Runner.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool delete_auto_start_task_for_this_user();

        [LibraryImport("Runner.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool is_auto_start_task_active_for_this_user();

        [LibraryImport("Runner.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool is_auto_start_task_run_elevated_for_this_user();

        [LibraryImport("Runner.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool get_auto_start_task_executable_path_for_this_user([Out] char[] buffer, uint cchBuffer);

        public static bool TryGetAutoStartTaskExecutablePath(char[] buffer)
        {
            return get_auto_start_task_executable_path_for_this_user(buffer, (uint)buffer.Length);
        }
    }
}
