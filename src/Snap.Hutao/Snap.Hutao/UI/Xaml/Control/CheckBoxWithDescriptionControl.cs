// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace Snap.Hutao.UI.Xaml.Control
{
    public partial class CheckBoxWithDescriptionControl : CheckBox
    {
        private CheckBoxWithDescriptionControl _checkBoxSubTextControl;

        public CheckBoxWithDescriptionControl()
        {
            _checkBoxSubTextControl = this;
            this.Loaded += CheckBoxSubTextControl_Loaded;
        }

        protected override void OnApplyTemplate()
        {
            Update();
            base.OnApplyTemplate();
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(Header))
            {
                AutomationProperties.SetName(this, Header);
            }
        }

        private void CheckBoxSubTextControl_Loaded(object sender, RoutedEventArgs e)
        {
            StackPanel panel = new StackPanel() { Orientation = Orientation.Vertical };
            panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock() { Text = Header, TextWrapping = TextWrapping.WrapWholeWords });

            // Add text box only if the description is not empty. Required for additional plugin options.
            if (!string.IsNullOrWhiteSpace(Description))
            {
                // ⚠️此处有无法恢复的致命错误，原因不明，怀疑与资源解析时机有关，暂时回退默认样式（仅在深色主题未选中状态显示有问题）
                //panel.Children.Add(new IsEnabledTextBlock() { Style = Application.Current.Resources["SecondaryIsEnabledTextBlockStyle"] as Style, Text = Description });

                IsEnabledTextBlock textBlock = new IsEnabledTextBlock() { Text = Description };

                // 延迟在 UI Dispatcher 中应用样式，避免在构造/早期生命周期强行解析资源导致 COM/WinRT 问题
                // 样式会在控件已加入视觉树后安全应用（无用的方法、不会进入 Style 设置 行58）
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    if (App.Current?.Resources is ResourceDictionary resources
                        && resources.ContainsKey("SecondaryIsEnabledTextBlockStyle")
                        && resources["SecondaryIsEnabledTextBlockStyle"] is Style secondaryStyle)
                    {
                        textBlock.Style = secondaryStyle;
                    }
                });


                panel.Children.Add(textBlock);
            }

            _checkBoxSubTextControl.Content = panel;
        }

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            "Header",
            typeof(string),
            typeof(CheckBoxWithDescriptionControl),
            new PropertyMetadata(default(string)));

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            "Description",
            typeof(string),
            typeof(CheckBoxWithDescriptionControl),
            new PropertyMetadata(default(string)));

        [Localizable(true)]
        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        [Localizable(true)]
        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }
    }
}
