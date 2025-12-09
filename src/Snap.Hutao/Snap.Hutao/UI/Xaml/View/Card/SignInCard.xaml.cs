// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Snap.Hutao.ViewModel.Abstraction;
using Snap.Hutao.ViewModel.Sign;

namespace Snap.Hutao.UI.Xaml.View.Card;

internal sealed partial class SignInCard : Button
{
    public SignInCard(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        this.InitializeViewModelSlim<SignInViewModel>(serviceProvider);
        this.DataContext<SignInViewModel>()?.AttachXamlElement(AwardScrollViewer);
    }

    private void CheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        this.DataContext<SignInViewModel>()?.IsAutoCheckIn = true;
    }

    private void CheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        this.DataContext<SignInViewModel>()?.IsAutoCheckIn = false;
    }
}