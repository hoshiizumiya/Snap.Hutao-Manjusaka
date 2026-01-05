// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Snap.Hutao.Core.Shell;

[SupportedOSPlatform("windows")]
static partial class NativeMethods
{
    // 使用更简单的 ShellExecuteW 签名，避免传递未被源生成封送支持的结构体
    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr ShellExecuteW(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShow);

    // GetLastError 函数声明
    [DllImport("kernel32.dll")]
    public static extern uint GetLastError();

    /// <summary>
    /// 检查当前进程是否以管理员身份运行
    /// </summary>
    /// <returns>如果以管理员身份运行则返回 true</returns>
    private static bool IsAdministrator()
    {
        try
        {
            // 使用 WindowsIdentity 检查当前用户的权限
            WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 以管理员身份重启当前应用程序
    /// </summary>
    /// <returns>如果成功请求重启则返回 true</returns>
    public static bool RestartAsAdministrator()
    {
        if (IsAdministrator())
        {
            // 如果已经是管理员，无需重启（already Disable button）
            Debug.WriteLine("Application is already running as administrator.");
            return true;
        }

        try
        {
            // 如果 Bootstrap 持有命名互斥锁，尽早释放它以避免新的提升进程在检测到锁时退出
            Bootstrap.UseNamedPipeRedirection();

            // 获取当前应用程序的完整路径
            // 将 Environment.ProcessPath 的可能 null 值安全处理，避免 CS8600
            string exeName = Environment.ProcessPath ?? string.Empty;

            // 使用 ShellExecuteW 请求提升（"runas"）
            IntPtr hInst = NativeMethods.ShellExecuteW(IntPtr.Zero, "runas", exeName, String.Empty, String.Empty, 1); // SW_SHOWNORMAL = 1

            // ShellExecute 返回值 > 32 表示成功
            if (hInst == IntPtr.Zero || hInst.ToInt64() <= 32)
            {
                uint errorCode = NativeMethods.GetLastError();
                throw new Win32Exception((int)errorCode);
            }

            Debug.WriteLine("Restart request sent. Closing current instance.");
            Application.Current.Exit();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to restart as administrator: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 以管理员身份启动当前应用程序
    /// </summary>
    /// <returns>如果成功请求重启则返回 true</returns>
    public static bool RestartAsAdministratorAtStart()
    {
        if (IsAdministrator())
        {
            // 如果已经是管理员，无需重启（already Disable button）
            Debug.WriteLine("Application is already running as administrator.");
            return true;
        }

        try
        {
            // 获取当前应用程序的完整路径
            // 将 Environment.ProcessPath 的可能 null 值安全处理，避免 CS8600
            string exeName = Environment.ProcessPath ?? string.Empty;

            // 使用 ShellExecuteW 请求提升（"runas"）
            IntPtr hInst = NativeMethods.ShellExecuteW(IntPtr.Zero, "runas", exeName, String.Empty, String.Empty, 1); // SW_SHOWNORMAL = 1

            // ShellExecute 返回值 > 32 表示成功
            if (hInst == IntPtr.Zero || hInst.ToInt64() <= 32)
            {
                uint errorCode = NativeMethods.GetLastError();
                throw new Win32Exception((int)errorCode);
            }

            Debug.WriteLine("Restart request sent. Closing current instance.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to restart as administrator: {ex.Message}");
            return false;
        }
    }

}