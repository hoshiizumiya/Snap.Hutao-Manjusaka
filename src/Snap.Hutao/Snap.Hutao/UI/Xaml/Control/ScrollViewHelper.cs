// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Snap.Hutao.UI.Xaml.Control;

[SuppressMessage("", "SH001")]
[DependencyProperty<UIElement>("RightPanel", IsAttached = true, TargetType = typeof(ScrollView))]
[DependencyProperty<object>(
    "ScrollToTopAssociatedObject",
    PropertyChangedCallbackName = nameof(OnScrollToTopAssociatedObjectChanged),
    IsAttached = true,
    TargetType = typeof(ScrollView))]
public sealed partial class ScrollViewHelper
{
    private static void OnScrollToTopAssociatedObjectChanged(DependencyObject dp, DependencyPropertyChangedEventArgs args)
    {
        if (dp is not ScrollView { IsLoaded: true } scrollView)
        {
            return;
        }

        if (args.OldValue != args.NewValue)
        {
            scrollView.ScrollPresenter.ScrollTo(0, 0);
        }
    }
}