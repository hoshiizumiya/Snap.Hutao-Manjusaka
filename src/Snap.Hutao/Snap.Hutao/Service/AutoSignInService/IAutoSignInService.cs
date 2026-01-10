// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Snap.Hutao.ViewModel.User;

namespace Snap.Hutao.Service.AutoSignIn;

internal interface IAutoSignInService
{
    /// <summary>
    /// 如果启用且存在当前用户，则尝试自动签到。
    /// 返回值表示是否触发了实际的“签到请求”（true = 已发起签到并成功；false = 未执行/无需执行/失败）。
    /// </summary>
    ValueTask<bool> RunAsync(CancellationToken token = default);

    /// <summary>
    /// 当前用户/账号发生变化时触发自动签到。
    /// 实现应自行判断是否需要签到（例如已签过/当日已执行过自动签到/未启用等）。
    /// </summary>
    ValueTask<bool> OnUserAndUidChangedAsync(UserAndUid userAndUid, CancellationToken token = default);
}