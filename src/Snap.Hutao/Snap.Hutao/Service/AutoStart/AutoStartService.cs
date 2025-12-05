// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.Setting;

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
        if (TryUseNativeHelper(out string? reason))
        {
            try
            {
                bool ok = NativeMethods.create_auto_start_task_for_this_user(runElevated ? 1 : 0);
                if (ok)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                try { SentrySdk.CaptureException(ex); } catch { }
                // fallback to schtasks
            }
        }

        // Fallback: use schtasks.exe for simplicity and less COM code. This may create a task that triggers on any user.
        string runLevel = runElevated ? "HIGHEST" : "LIMITED";

        // Limit the task to the current user by specifying /RU "<DOMAIN\Username>"
        string runUser = $"{Environment.UserDomainName}\\{Environment.UserName}";

        // Create task that runs at logon for the current user only
        string arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL {runLevel} /RU \"{runUser}\" /IT /F";

        Debug.WriteLine($"Registering autostart task with arguments: {arguments}");

        ProcessStartInfo psi = new()
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process proc = Process.Start(psi)!;
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            // 将 stdout/stderr 一并记录，便于后续定位权限或参数问题
            throw HutaoException.InvalidOperation($"Failed to register autostart task. ExitCode: {proc.ExitCode}. stdout: {stdout}. stderr: {stderr}");
        }
    }

    private void UnregisterAutoStartTask()
    {
        // Prefer native helper
        if (TryUseNativeHelper(out _))
        {
            try
            {
                bool ok = NativeMethods.delete_auto_start_task_for_this_user();
                if (ok)
                {
                    return;
                }
            }
            catch
            {
                // ignore and fallback
            }
        }

        ProcessStartInfo psi = new()
        {
            FileName = "schtasks.exe",
            Arguments = $"/Delete /TN \"{TaskName}\" /F",
            CreateNoWindow = true,
            UseShellExecute = false,
        };

        using Process proc = Process.Start(psi)!;
        proc.WaitForExit();

        // ignore failures - task might not exist
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
    }
}
