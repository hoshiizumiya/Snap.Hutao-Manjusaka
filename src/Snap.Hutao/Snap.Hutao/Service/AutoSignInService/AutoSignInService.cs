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
        string lastAttemptKey = AutoSignInLastAttemptDayKeyPrefix + uidString;
        string serverDayKey = GetServerDayKey(userAndUid.Uid);

        // Dedupe by server day (not local day).
        if (LocalSetting.Get(lastAttemptKey, string.Empty) == serverDayKey)
        {
            return false;
        }

        try
        {
            await taskContext.SwitchToBackgroundAsync();

            // Always check current server sign-in state before attempting to sign.
            // This prevents repeated sign requests on repeated app launches / user switching.
            Response<SignInRewardInfo> infoResponse = await GetSignInInfoAsync(userAndUid, token).ConfigureAwait(false);
            if (infoResponse is { ReturnCode: 0, Data: { } data } && data.IsSign)
            {
                LocalSetting.Set(lastAttemptKey, serverDayKey);
                messenger.Send(new SignInDataChangedMessage(userAndUid, postSign: false));
                return false;
            }

            bool success = await signInService.ClaimSignInRewardAsync(userAndUid, token).ConfigureAwait(false);

            // Mark attempted for this server day regardless of result to avoid spamming.
            LocalSetting.Set(lastAttemptKey, serverDayKey);

            messenger.Send(new SignInDataChangedMessage(userAndUid, postSign: success));
            return success;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
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