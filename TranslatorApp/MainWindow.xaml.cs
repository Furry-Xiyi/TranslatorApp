using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using TranslatorApp.Pages;
using TranslatorApp.Services;

namespace TranslatorApp;

public sealed partial class MainWindow : Window
{
    private bool _activatedOnce = false;
    private bool _welcomeShown = false;

    public MainWindow()
    {
        InitializeComponent();

        // 默认进入在线翻译页
        NavView.SelectedItem = Nav_Online;
        NavigateTo(typeof(OnlineTranslatePage));

        // Window 没有 Loaded 事件，这里用 Activated 首次触发
        Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_activatedOnce) return;
        _activatedOnce = true;

        // 首次进入且没有任何 API Key 时显示欢迎对话框
        if (!_welcomeShown && !SettingsService.HasAnyApiKey())
        {
            _welcomeShown = true;
            await ShowWelcomeDialogAsync();
        }
    }

    private void NavigateTo(Type pageType, object? param = null)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType, param);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateTo(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem nvi)
        {
            switch (nvi.Tag as string)
            {
                case "OnlineTranslatePage":
                    NavigateTo(typeof(OnlineTranslatePage));
                    break;
                case "WordLookupPage":
                    NavigateTo(typeof(WordLookupPage));
                    break;
                case "FavoritesPage":
                    NavigateTo(typeof(FavoritesPage));
                    break;
            }
        }
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        TryGoBack();
    }

    private void BackAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (TryGoBack()) args.Handled = true;
    }

    private void ForwardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ContentFrame.CanGoForward)
        {
            ContentFrame.GoForward();
            args.Handled = true;
        }
    }

    private bool TryGoBack()
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
            return true;
        }
        return false;
    }

    private async Task ShowWelcomeDialogAsync()
    {
        var root = (FrameworkElement)Content;

        var dialog = new ContentDialog
        {
            XamlRoot = root.XamlRoot,
            Title = "欢迎使用翻译",
            PrimaryButtonText = "去填写",
            CloseButtonText = "稍后",
            DefaultButton = ContentDialogButton.Primary
        };

        var expander = new Expander
        {
            Header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new SymbolIcon(Symbol.Help),
                    new StackPanel
                    {
                        Margin = new Thickness(12,0,0,0),
                        Children =
                        {
                            new TextBlock { Text = "如何获取 API 密钥？", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                            new TextBlock { Text = "点击展开后链接前往获取", Opacity = 0.7 }
                        }
                    }
                }
            },
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                Children =
                {
                    new HyperlinkButton { Content = "Bing API 申请", NavigateUri = new Uri("https://TODO-替换-Bing-API-链接") },
                    new HyperlinkButton { Content = "百度翻译 API 申请", NavigateUri = new Uri("https://TODO-替换-百度-API-链接") },
                    new HyperlinkButton { Content = "有道翻译 API 申请", NavigateUri = new Uri("https://TODO-替换-有道-API-链接") }
                }
            },
            IsExpanded = false
        };

        dialog.Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "使用翻译需要填写 API 密钥", Opacity = 0.8, Margin = new Thickness(0,0,0,8) },
                expander
            }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // 转到设置页
            NavView.SelectedItem = null;
            NavigateTo(typeof(SettingsPage));
            NavView.IsSettingsVisible = true;
            (NavView.SettingsItem as FrameworkElement)?.Focus(FocusState.Programmatic);
        }
    }
}