// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.ViewModel.User;

namespace Snap.Hutao.Service.AutoSignIn;

/// <summary>
/// Triggers auto sign-in on <see cref="UserAndUidChangedMessage"/>.
/// This ensures both "user switched" and "uid switched" paths are covered.
/// </summary>
[Service(ServiceLifetime.Singleton)]
internal sealed partial class AutoSignInTriggerService : IRecipient<UserAndUidChangedMessage>
{
    private readonly IAutoSignInService autoSignInService;

    [GeneratedConstructor]
    public partial AutoSignInTriggerService(IServiceProvider serviceProvider);

    public void Receive(UserAndUidChangedMessage message)
    {
        if (message.UserAndUid is { } userAndUid)
        {
            autoSignInService.OnUserAndUidChangedAsync(userAndUid).AsTask().SafeForget();
        }
    }
}
