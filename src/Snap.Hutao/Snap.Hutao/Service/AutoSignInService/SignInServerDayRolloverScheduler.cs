// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.ViewModel.User;
using Snap.Hutao.Web.Hoyolab;

namespace Snap.Hutao.Service.AutoSignIn;

[Service(ServiceLifetime.Singleton)]
internal sealed partial class SignInServerDayRolloverScheduler : IRecipient<UserAndUidChangedMessage>, IDisposable
{
    private readonly IAutoSignInService autoSignInService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    private CancellationTokenSource? scheduleCts;

    [GeneratedConstructor]
    public partial SignInServerDayRolloverScheduler(IServiceProvider serviceProvider);

    public void Dispose()
    {
        scheduleCts?.Cancel();
        scheduleCts?.Dispose();
        scheduleCts = null;
    }

    public void Receive(UserAndUidChangedMessage message)
    {
        if (message.UserAndUid is not { } userAndUid)
        {
            CancelSchedule();
            return;
        }

        Reschedule(userAndUid);
    }

    private void CancelSchedule()
    {
        scheduleCts?.Cancel();
        scheduleCts?.Dispose();
        scheduleCts = null;
    }

    private void Reschedule(UserAndUid userAndUid)
    {
        CancelSchedule();

        scheduleCts = new();
        CancellationToken token = scheduleCts.Token;

        // Fire & forget: schedule a tick at next server-day boundary.
        RunAsync(userAndUid, token).SafeForget();
    }

    [SuppressMessage("", "SH003")]
    private async Task RunAsync(UserAndUid userAndUid, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TimeSpan offset = PlayerUid.GetRegionTimeZoneUtcOffsetForRegion(userAndUid.Uid.Region);
            DateTimeOffset serverNow = DateTimeOffset.UtcNow.ToOffset(offset);

            DateTimeOffset nextServerDay = new(serverNow.Date.AddDays(1), offset);
            TimeSpan delay = nextServerDay - serverNow;
            if (delay < TimeSpan.FromSeconds(1))
            {
                // In case of clock drift.
                delay = TimeSpan.FromSeconds(1);
            }

            await Task.Delay(delay, token).ConfigureAwait(false);

            // At (or shortly after) server day rollover, refresh sign-in status.
            await taskContext.SwitchToBackgroundAsync();

            bool signed = await autoSignInService.OnUserAndUidChangedAsync(userAndUid, token).ConfigureAwait(false);

            // Notify consumers to refresh UI/state based on sign-in data.
            messenger.Send(new SignInDataChangedMessage(userAndUid, postSign: signed));
        }
    }
}
