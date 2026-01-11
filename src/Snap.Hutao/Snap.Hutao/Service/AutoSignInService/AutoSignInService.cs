// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.DependencyInjection.Abstraction;
using Snap.Hutao.Core.Setting;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Service.SignIn;
using Snap.Hutao.Service.User;
using Snap.Hutao.ViewModel.User;
using Snap.Hutao.Web.Hoyolab;
using Snap.Hutao.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Snap.Hutao.Web.Response;

namespace Snap.Hutao.Service.AutoSignIn;

[Service(ServiceLifetime.Singleton, typeof(IAutoSignInService))]
internal sealed partial class AutoSignInService : IAutoSignInService
{
    private const string AutoSignInSettingKey = "SignIn.AutoSignInEnabled";
    private const string AutoSignInLastAttemptDayKeyPrefix = "SignIn.AutoSignIn.LastAttemptDayKey.";
    private const string AutoSignInLastFailureUtcTicksPrefix = "SignIn.AutoSignIn.LastFailureUtcTicks.";

    private static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(10);

    private readonly IServiceProvider serviceProvider;
    private readonly ISignInService signInService;
    private readonly IUserService userService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    [GeneratedConstructor]
    public partial AutoSignInService(IServiceProvider serviceProvider);

    public async ValueTask<bool> RunAsync(CancellationToken token = default)
    {
        if (!LocalSetting.Get(AutoSignInSettingKey, true))
        {
            return false;
        }

        if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
        {
            return false;
        }

        return await OnUserAndUidChangedAsync(userAndUid, token).ConfigureAwait(false);
    }

    public async ValueTask<bool> OnUserAndUidChangedAsync(UserAndUid userAndUid, CancellationToken token = default)
    {
        if (!LocalSetting.Get(AutoSignInSettingKey, true))
        {
            return false;
        }

        string uidString = userAndUid.Uid.ToString();
        string completedDayKeySetting = AutoSignInLastAttemptDayKeyPrefix + uidString;
        string lastFailureTicksKey = AutoSignInLastFailureUtcTicksPrefix + uidString;
        string serverDayKey = GetServerDayKey(userAndUid.Uid);

        // Dedupe by server day when we already know the user is signed or we have signed successfully.
        if (LocalSetting.Get(completedDayKeySetting, string.Empty) == serverDayKey)
        {
            return false;
        }

        // Throttle repeated failures to reduce spam while still allowing retries later.
        long lastFailureTicks = LocalSetting.Get(lastFailureTicksKey, 0L);
        if (lastFailureTicks != 0)
        {
            DateTimeOffset lastFailure = new(lastFailureTicks, TimeSpan.Zero);
            if (DateTimeOffset.UtcNow - lastFailure < FailureCooldown)
            {
                return false;
            }
        }

        try
        {
            await taskContext.SwitchToBackgroundAsync();

            // 1) Check current server sign-in state first.
            Response<SignInRewardInfo> infoResponse = await GetSignInInfoAsync(userAndUid, token).ConfigureAwait(false);
            if (infoResponse is { ReturnCode: 0, Data: { } data } && data.IsSign)
            {
                LocalSetting.Set(completedDayKeySetting, serverDayKey);
                messenger.Send(new SignInDataChangedMessage(userAndUid, postSign: false));
                return false;
            }

            // If GetInfo failed (network/cookie/etc.), do not lock the whole day.
            // We still attempt sign-in, because ClaimSignInRewardAsync already has robust handling.
            bool success = await signInService.ClaimSignInRewardAsync(userAndUid, token).ConfigureAwait(false);

            if (success)
            {
                LocalSetting.Set(completedDayKeySetting, serverDayKey);
                LocalSetting.Set(lastFailureTicksKey, 0L);
            }
            else
            {
                LocalSetting.Set(lastFailureTicksKey, DateTimeOffset.UtcNow.Ticks);
            }

            messenger.Send(new SignInDataChangedMessage(userAndUid, postSign: success));
            return success;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            LocalSetting.Set(lastFailureTicksKey, DateTimeOffset.UtcNow.Ticks);
            messenger.Send(InfoBarMessage.Error(ex));
            return false;
        }
    }

    private static string GetServerDayKey(PlayerUid uid)
    {
        TimeSpan offset = PlayerUid.GetRegionTimeZoneUtcOffsetForRegion(uid.Region);
        DateTimeOffset serverNow = DateTimeOffset.UtcNow.ToOffset(offset);
        return $"{uid.Region.Value}:{serverNow:yyyy-MM-dd}";
    }

    private async ValueTask<Response<SignInRewardInfo>> GetSignInInfoAsync(UserAndUid userAndUid, CancellationToken token)
    {
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            ISignInClient signInClient = scope.ServiceProvider
                .GetRequiredService<IOverseaSupportFactory<ISignInClient>>()
                .Create(userAndUid.IsOversea);

            return await signInClient.GetInfoAsync(userAndUid, token).ConfigureAwait(false);
        }
    }
}