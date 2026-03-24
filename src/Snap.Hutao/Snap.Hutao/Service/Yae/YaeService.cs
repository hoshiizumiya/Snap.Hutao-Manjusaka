// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.Core.LifeCycle.InterProcess.Yae;
using Snap.Hutao.Factory.ContentDialog;
using Snap.Hutao.Model.InterChange.Achievement;
using Snap.Hutao.Model.InterChange.Inventory;
using Snap.Hutao.Service.Game;
using Snap.Hutao.Service.Game.FileSystem;
using Snap.Hutao.Service.Game.Launching;
using Snap.Hutao.Service.Game.Launching.Context;
using Snap.Hutao.Service.Game.Launching.Invoker;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Service.User;
using Snap.Hutao.Service.Yae.Achievement;
using Snap.Hutao.Service.Yae.Metadata;
using Snap.Hutao.Service.Yae.PlayerStore;
using Snap.Hutao.ViewModel.Game;
using Snap.Hutao.ViewModel.User;
using System.Diagnostics;
using System.IO;

namespace Snap.Hutao.Service.Yae;

[Service(ServiceLifetime.Singleton, typeof(IYaeService))]
internal sealed partial class YaeService : IYaeService
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly IYaeMetadataService yaeMetadataService;
    private readonly IUserService userService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    [GeneratedConstructor]
    public partial YaeService(IServiceProvider serviceProvider);

    public async ValueTask<UIAF?> GetAchievementAsync(IViewModelSupportLaunchExecution viewModel)
    {
        ContentDialog dialog = await contentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.ServiceYaeWaitForGameResponseMessage)
            .ConfigureAwait(false);

        using (await contentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
        {
            await taskContext.SwitchToBackgroundAsync();
            using (YaeDataArrayReceiver receiver = new())
            {
                try
                {
                    UserAndUid? userAndUid = await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false);
                    LaunchExecutionInvocationContext context = new()
                    {
                        ViewModel = viewModel,
                        ServiceProvider = serviceProvider,
                        LaunchOptions = serviceProvider.GetRequiredService<LaunchOptions>(),
                        Identity = GameIdentity.Create(userAndUid, viewModel.GameAccount),
                    };

                    TargetNativeConfiguration? config = await TryGetTargetNativeConfigurationAsync(context).ConfigureAwait(false);
                    if (config is null)
                    {
                        return default;
                    }

                    await new YaeLaunchExecutionInvoker(config, receiver).InvokeAsync(context).ConfigureAwait(false);

                    UIAF? uiaf = default;
                    foreach (YaeData data in receiver.Array)
                    {
                        using (data)
                        {
                            if (data.Kind is YaeCommandKind.ResponseAchievement)
                            {
                                Debug.Assert(uiaf is null);
                                uiaf = AchievementParser.Parse(data.Bytes);
                            }
                        }
                    }

                    return uiaf;
                }
                catch (Exception ex)
                {
                    messenger.Send(InfoBarMessage.Error(ex));
                    return default;
                }
            }
        }
    }

    public async ValueTask<UIIF?> GetInventoryAsync(IViewModelSupportLaunchExecution viewModel)
    {
        ContentDialog dialog = await contentDialogFactory
            .CreateForIndeterminateProgressAsync(SH.ServiceYaeWaitForGameResponseMessage)
            .ConfigureAwait(false);

        using (await contentDialogFactory.BlockAsync(dialog).ConfigureAwait(false))
        {
            await taskContext.SwitchToBackgroundAsync();
            UIIF? uiif = default;
            Dictionary<InterestedPropType, double> propMap = [];
            using (YaeDataArrayReceiver receiver = new())
            {
                try
                {
                    UserAndUid? userAndUid = await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false);
                    LaunchExecutionInvocationContext context = new()
                    {
                        ViewModel = viewModel,
                        ServiceProvider = serviceProvider,
                        LaunchOptions = serviceProvider.GetRequiredService<LaunchOptions>(),
                        Identity = GameIdentity.Create(userAndUid, viewModel.GameAccount),
                    };

                    TargetNativeConfiguration? config = await TryGetTargetNativeConfigurationAsync(context).ConfigureAwait(false);
                    if (config is null)
                    {
                        return default;
                    }

                    await new YaeLaunchExecutionInvoker(config, receiver).InvokeAsync(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    messenger.Send(InfoBarMessage.Error(ex));
                    return default;
                }

                foreach (YaeData data in receiver.Array)
                {
                    using (data)
                    {
                        switch (data.Kind)
                        {
                            case YaeCommandKind.ResponsePlayerStore:
                                Debug.Assert(uiif is null);
                                uiif = PlayerStoreParser.Parse(data.Bytes);
                                break;
                            case YaeCommandKind.ResponsePlayerProp:
                                {
                                    ref readonly YaePropertyTypeValue typeValue = ref data.PropertyTypeValue;
                                    propMap.Add(typeValue.Type, typeValue.Value);
                                    break;
                                }
                        }
                    }
                }
            }

            if (uiif is null)
            {
                return default;
            }

            // Unfortunately, we store data in uint rather than double, so we have to truncate the value.
            double count = propMap.GetValueOrDefault(InterestedPropType.PlayerSCoin) - propMap.GetValueOrDefault(InterestedPropType.PlayerWaitSubSCoin);
            UIIFItem mora = UIIFItem.From(202U, (uint)Math.Clamp(count, uint.MinValue, uint.MaxValue));

            return uiif.WithList([mora, .. uiif.List]);
        }
    }

    private async ValueTask<TargetNativeConfiguration?> TryGetTargetNativeConfigurationAsync(LaunchExecutionInvocationContext context)
    {
        const string LockTrace = $"{nameof(YaeService)}.{nameof(TryGetTargetNativeConfigurationAsync)}";

        if (context.LaunchOptions.TryGetGameFileSystem(LockTrace, out IGameFileSystem? gameFileSystem) is not GameFileSystemErrorKind.None)
        {
            context.ServiceProvider.GetRequiredService<IMessenger>().Send(InfoBarMessage.Error(SH.ServiceYaeGetGameVersionFailed));
        }

        if (gameFileSystem is null)
        {
            return default;
        }

        using (gameFileSystem)
        {
            if (!TryGetGameExecutableHash(gameFileSystem.GameFilePath, out uint hash))
            {
                messenger.Send(InfoBarMessage.Error(SH.ServiceYaeGetGameVersionFailed));
                return default;
            }

            YaeNativeLibConfig? nativeConfig = await yaeMetadataService.GetNativeLibConfigAsync().ConfigureAwait(false);
            if (nativeConfig is null)
            {
                messenger.Send(InfoBarMessage.Error(SH.ServiceYaeGetGameVersionFailed));
                return default;
            }

            if (!nativeConfig.MethodRva.TryGetValue(hash, out MethodRva? methodRva))
            {
                messenger.Send(InfoBarMessage.Error(SH.ServiceYaeGetGameVersionFailed));
                return default;
            }

            return TargetNativeConfiguration.Create(nativeConfig.StoreCmdId, nativeConfig.AchievementCmdId, methodRva);
        }
    }

    private static bool TryGetGameExecutableHash(string gameFilePath, out uint hash)
    {
        try
        {
            Span<byte> buffer = stackalloc byte[0x10000];
            using FileStream stream = File.OpenRead(gameFilePath);
            int read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            if (read < buffer.Length)
            {
                hash = default;
                return false;
            }

            hash = Crc32.Compute(buffer);
            return true;
        }
        catch (IOException)
        {
            hash = default;
            return false;
        }
    }
}
