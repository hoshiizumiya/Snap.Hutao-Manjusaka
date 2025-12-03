using Microsoft.Win32;
using Snap.Hutao.Core.Setting;
using Snap.Hutao.Core;
using Snap.Hutao.Core.ExceptionService;
using System.Diagnostics;
using System.IO;

namespace Snap.Hutao.Service;

[Service(ServiceLifetime.Singleton)]
internal sealed class AutoStartService
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
        LocalSetting.Set(SettingKeys.StartupEnabled, enable);
        LocalSetting.Set(SettingKeys.RunElevated, runElevated);

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
        }
        catch (Exception ex)
        {
            try { SentrySdk.CaptureException(ex); } catch { }
            throw;
        }
    }

    private void RegisterAutoStartTask(bool runElevated)
    {
        // Prefer using schtasks.exe for simplicity and less COM code.
        // Build command to create or update task for current user.    {
        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            throw HutaoException.InvalidOperation("Cannot find executable path to register autostart task.");
        }

        // schtasks arguments
        // /RL HIGHEST for elevated, otherwise LIMITED
        string runLevel = runElevated ? "HIGHEST" : "LIMITED";

        // /RU <User> omitted to use current user interactive token with /RL HIGHEST and /F
        // Create task that runs at logon
        string arguments = $"/Create /TN \"{TaskName}\" /TR \"{exePath}\" /SC ONLOGON /RL {runLevel} /F";

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
}
