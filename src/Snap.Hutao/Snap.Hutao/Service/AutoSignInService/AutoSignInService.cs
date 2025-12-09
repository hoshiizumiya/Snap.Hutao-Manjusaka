// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.Core.Setting;
using Snap.Hutao.Service.Notification;
using Snap.Hutao.Service.SignIn;
using Snap.Hutao.Service.User;
using Snap.Hutao.ViewModel.User;

namespace Snap.Hutao.Service.AutoSignIn;

[Service(ServiceLifetime.Singleton, typeof(IAutoSignInService))]
internal sealed partial class AutoSignInService : IAutoSignInService
{
    private const string AutoSignInSettingKey = "SignIn.AutoSignInEnabled";

    private readonly ISignInService signInService;
    private readonly IUserService userService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    [GeneratedConstructor]
    public partial AutoSignInService(IServiceProvider serviceProvider);

    public async ValueTask<bool> RunAsync(CancellationToken token = default)
    {
        bool enabled = LocalSetting.Get(AutoSignInSettingKey, true);
        if (!enabled)
        {
            return false;
        }

        if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
        {
            messenger.Send(InfoBarMessage.Warning(SH.MustSelectUserAndUid));
            return false;
        }

        try
        {
            await taskContext.SwitchToBackgroundAsync();
            return await signInService.ClaimSignInRewardAsync(userAndUid, token).ConfigureAwait(false);
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
}