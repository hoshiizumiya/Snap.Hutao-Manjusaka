// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core;
using Snap.Hutao.Core.Diagnostics;
using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.LifeCycle.InterProcess.Yae;
using Snap.Hutao.Factory.Process;
using Snap.Hutao.Service.Game;
using Snap.Hutao.Service.Game.Island;
using Snap.Hutao.Service.Game.Launching.Context;
using Snap.Hutao.Service.Yae.Achievement;
using Snap.Hutao.Win32;
using Snap.Hutao.Win32.Foundation;
using System.IO;

namespace Snap.Hutao.Service.Game.Launching.Handler;

internal sealed class LaunchExecutionYaeNamedPipeHandler : AbstractLaunchExecutionHandler
{
    private readonly TargetNativeConfiguration config;
    private readonly YaeDataArrayReceiver receiver;

    public LaunchExecutionYaeNamedPipeHandler(TargetNativeConfiguration config, YaeDataArrayReceiver receiver)
    {
        this.config = config;
        this.receiver = receiver;
    }

    public override async ValueTask ExecuteAsync(LaunchExecutionContext context)
    {
        if (!HutaoRuntime.IsProcessElevated)
        {
            context.Process.Kill();
            HutaoException.NotSupported(SH.ServiceGameLaunchingHandlerEmbeddedYaeClientNotElevated);
        }

        string dataFolderYaePath = Path.Combine(HutaoRuntime.DataDirectory, "YaeAchievementLib.dll");
        InstalledLocation.CopyFileFromApplicationUri("ms-appx:///YaeAchievementLib.dll", dataFolderYaePath);

        // 直接使用创建的游戏进程
        int actualProcessId = context.Process.Id;
        if (actualProcessId == 0)
        {
            throw HutaoException.Throw("游戏进程未正确创建");
        }

        try
        {
            DllInjectionUtilities.InjectUsingRemoteThread(dataFolderYaePath, "YaeMain", actualProcessId);
        }
        catch (Exception ex)
        {
            // Windows Defender Application Control
            if (HutaoNative.IsWin32(ex.HResult, WIN32_ERROR.ERROR_SYSTEM_INTEGRITY_POLICY_VIOLATION))
            {
                throw HutaoException.Throw(SH.ServiceGameLaunchingHandlerEmbeddedYaeErrorSystemIntegrityPolicyViolation);
            }

            // Access Denied (0x80070005) - 权限不足，无法在远程进程中分配内存
            if (ex.HResult == unchecked((int)0x80070005))
            {
                throw HutaoException.Throw($"无法在游戏进程中注入 DLL (访问被拒绝)。\n\n" +
                    $"可能的原因：\n" +
                    $"1. 游戏进程的完整性级别高于 Snap Hutao\n" +
                    $"2. Windows Defender 或其他安全软件阻止了注入\n" +
                    $"解决方法：\n" +
                    $"1. 检查 Windows Defender 设置，将 Snap Hutao 添加到排除列表\n" +
                    $"2. 以管理员身份运行 Snap Hutao\n" +
                    $"3. 检查是否有其他安全软件（如 360、火绒等）干扰");
            }

            // 游戏进程由直接启动，已经是运行状态
            // InjectUsingWindowsHook2 需要手动恢复主线程，但 DiagnosticsProcess 不支持 ResumeMainThread
            // 这里不使用 InjectUsingWindowsHook2
            throw new InvalidOperationException($"无法注入 DLL: {ex.Message}. 请确保没有启用 Windows Defender Application Control 或其他安全限制。", ex);
        }

        try
        {
            // 获取游戏进程用于命名管道服务器
            IProcess actualProcess = ProcessFactory.TryGetById(actualProcessId, out IProcess? process)
                ? process
                : throw HutaoException.Throw($"无法获取进程 ID {actualProcessId}");

            // 已经是运行状态，不需要恢复主线程
#pragma warning disable CA2007
            await using (YaeNamedPipeServer server = new(context.ServiceProvider, actualProcess, config, supportsResumeMainThread: false))
#pragma warning restore CA2007
            {
                receiver.Array = await server.GetDataArrayAsync().ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
