// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.Diagnostics;
using Snap.Hutao.Core.ExceptionService;
using Snap.Hutao.Core.LifeCycle.InterProcess.Yae;
using Snap.Hutao.Factory.Progress;
using Snap.Hutao.Factory.Process;
using Snap.Hutao.Service.Game.FileSystem;
using Snap.Hutao.Service.Game.Launching.Context;
using Snap.Hutao.Service.Game.Launching.Handler;
using Snap.Hutao.Service.Game.Package;
using Snap.Hutao.Service.Yae.Achievement;

namespace Snap.Hutao.Service.Game.Launching.Invoker;

internal sealed class YaeLaunchExecutionInvoker : AbstractLaunchExecutionInvoker
{
    public YaeLaunchExecutionInvoker(TargetNativeConfiguration config, YaeDataArrayReceiver receiver)
    {
        Handlers =
        [
            new LaunchExecutionGameLifeCycleHandler(resume: false),
            new LaunchExecutionChannelOptionsHandler(),
            new LaunchExecutionGameResourceHandler(false),
            new LaunchExecutionGameIdentityHandler(),
            new LaunchExecutionGameProcessStartHandler(),
            new LaunchExecutionYaeNamedPipeHandler(config, receiver),
        ];
    }

    protected override IProcess? CreateProcess(BeforeLaunchExecutionContext beforeContext)
    {
        return GameProcessFactory.CreateForEmbeddedYae(beforeContext);
    }

    protected override bool ShouldWaitForProcessExit { get => false; }

    protected override bool ShouldSpinWaitGameExitAfterInvoke { get => false; }

    public async ValueTask InvokeAsync(LaunchExecutionInvocationContext context)
    {
        ITaskContext taskContext = context.ServiceProvider.GetRequiredService<ITaskContext>();

        string lockTrace = $"{GetType().Name}.{nameof(InvokeAsync)}";
        context.LaunchOptions.TryGetGameFileSystem(lockTrace, out IGameFileSystem? gameFileSystem);
        ArgumentNullException.ThrowIfNull(gameFileSystem);

        using (GameFileSystemReference fileSystemReference = new(gameFileSystem))
        {
            if (context.ViewModel.TargetScheme is not { } targetScheme)
            {
                throw HutaoException.InvalidOperation(SH.ViewModelLaunchGameSchemeNotSelected);
            }

            if (context.ViewModel.CurrentScheme is not { } currentScheme)
            {
                throw HutaoException.InvalidOperation(SH.ServiceGameLaunchExecutionCurrentSchemeNull);
            }

            IProgress<LaunchStatus?> progress = CreateStatusProgress(context.ServiceProvider);

            BeforeLaunchExecutionContext beforeContext = new()
            {
                ViewModel = context.ViewModel,
                Progress = progress,
                ServiceProvider = context.ServiceProvider,
                TaskContext = taskContext,
                FileSystem = fileSystemReference,
                HoyoPlay = context.ServiceProvider.GetRequiredService<IHoyoPlayService>(),
                Messenger = context.ServiceProvider.GetRequiredService<IMessenger>(),
                LaunchOptions = context.LaunchOptions,
                CurrentScheme = currentScheme,
                TargetScheme = targetScheme,
                Identity = context.Identity,
            };

            foreach (ILaunchExecutionHandler handler in Handlers)
            {
                await handler.BeforeAsync(beforeContext).ConfigureAwait(false);
            }

            fileSystemReference.Exchange(beforeContext.FileSystem);

            // Yae注入功能不依赖unlockfps.exe，总是创建游戏进程
            IProcess? process = CreateProcess(beforeContext);

            using (process)
            {
                if (process is null)
                {
                    return;
                }

                LaunchExecutionContext executionContext = new()
                {
                    Progress = progress,
                    ServiceProvider = context.ServiceProvider,
                    TaskContext = taskContext,
                    Messenger = context.ServiceProvider.GetRequiredService<IMessenger>(),
                    LaunchOptions = context.LaunchOptions,
                    Process = process,
                    IsOversea = targetScheme.IsOversea,
                };

                foreach (ILaunchExecutionHandler handler in Handlers)
                {
                    await handler.ExecuteAsync(executionContext).ConfigureAwait(false);
                }
            }

            AfterLaunchExecutionContext afterContext = new()
            {
                ServiceProvider = context.ServiceProvider,
                TaskContext = taskContext,
            };

            foreach (ILaunchExecutionHandler handler in Handlers)
            {
                await handler.AfterAsync(afterContext).ConfigureAwait(false);
            }
        }
    }

    private static IProgress<LaunchStatus?> CreateStatusProgress(IServiceProvider serviceProvider)
    {
        IProgressFactory progressFactory = serviceProvider.GetRequiredService<IProgressFactory>();
        LaunchStatusOptions options = serviceProvider.GetRequiredService<LaunchStatusOptions>();
        return progressFactory.CreateForMainThread<LaunchStatus?, LaunchStatusOptions>(static (status, options) => options.LaunchStatus = status, options);
    }
}