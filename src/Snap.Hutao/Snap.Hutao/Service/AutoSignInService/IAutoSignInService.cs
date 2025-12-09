// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

namespace Snap.Hutao.Service.AutoSignIn;

internal interface IAutoSignInService
{
    /// <summary>
    /// 如果启用且存在当前用户，则尝试自动签到。
    /// 返回值表示是否尝试并成功签到（true = 成功签入；false = 未执行或失败/已签过）。
    /// </summary>
    ValueTask<bool> RunAsync(CancellationToken token = default);
}