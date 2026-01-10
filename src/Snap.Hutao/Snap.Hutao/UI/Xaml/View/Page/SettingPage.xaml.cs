// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.UI.Xaml.Control;
using Snap.Hutao.ViewModel.Setting;
using System.Numerics;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;

namespace Snap.Hutao.UI.Xaml.View.Page;

internal sealed partial class SettingPage : ScopedPage
{
    public SettingPage()
    {
        InitializeComponent();
        //Loaded += OnLoaded;
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<SettingViewModel>();
        this.DataContext<SettingViewModel>()?.AttachXamlElement(RootScrollViewer, GachaLogBorder);
    }

    //private void OnLoaded(object sender, RoutedEventArgs e)
    //{
    //    if (RootScrollView is null || RootScrollViewerAnnotatedScrollBar is null)
    //    {
    //        return;
    //    }

    //    RootScrollView.ScrollPresenter.VerticalScrollController = RootScrollViewerAnnotatedScrollBar.ScrollController;

    //    // Only add scroll velocity when user scrolls (mouse wheel). Subscribe to PointerWheelChanged.
    //    RootScrollView.PointerWheelChanged += OnPointerWheelChanged;
    //}

    //private void OnPointerWheelChanged(object? sender, PointerRoutedEventArgs e)
    //{
    //    if (RootScrollView?.ScrollPresenter is null)
    //    {
    //        return;
    //    }

    //    try
    //    {
    //        // Mouse wheel delta per notch is typically 120. Convert to a velocity value.
    //        PointerPoint point = e.GetCurrentPoint(RootScrollView);
    //        int wheelDelta = point.Properties.MouseWheelDelta;

    //        // Scale factor chosen empirically; adjust if needed.
    //        float velocityY = wheelDelta * 100f;

    //        RootScrollView.ScrollPresenter.AddScrollVelocity(new Vector2(0f, velocityY), null);
    //    }
    //    catch
    //    {
    //        // Ignore if API not available or call fails.
    //    }
    //}

    //private void RootScrollViewerAnnotatedScrollBar_DetailLabelRequested(AnnotatedScrollBar sender, AnnotatedScrollBarDetailLabelRequestedEventArgs args)
    //{
    //}
}