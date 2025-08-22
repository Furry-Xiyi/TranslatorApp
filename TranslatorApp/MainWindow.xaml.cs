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

        // Ĭ�Ͻ������߷���ҳ
        NavView.SelectedItem = Nav_Online;
        NavigateTo(typeof(OnlineTranslatePage));

        // Window û�� Loaded �¼��������� Activated �״δ���
        Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_activatedOnce) return;
        _activatedOnce = true;

        // �״ν�����û���κ� API Key ʱ��ʾ��ӭ�Ի���
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
            Title = "��ӭʹ�÷���",
            PrimaryButtonText = "ȥ��д",
            CloseButtonText = "�Ժ�",
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
                            new TextBlock { Text = "��λ�ȡ API ��Կ��", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                            new TextBlock { Text = "���չ��������ǰ����ȡ", Opacity = 0.7 }
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
                    new HyperlinkButton { Content = "Bing API ����", NavigateUri = new Uri("https://TODO-�滻-Bing-API-����") },
                    new HyperlinkButton { Content = "�ٶȷ��� API ����", NavigateUri = new Uri("https://TODO-�滻-�ٶ�-API-����") },
                    new HyperlinkButton { Content = "�е����� API ����", NavigateUri = new Uri("https://TODO-�滻-�е�-API-����") }
                }
            },
            IsExpanded = false
        };

        dialog.Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "ʹ�÷�����Ҫ��д API ��Կ", Opacity = 0.8, Margin = new Thickness(0,0,0,8) },
                expander
            }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // ת������ҳ
            NavView.SelectedItem = null;
            NavigateTo(typeof(SettingsPage));
            NavView.IsSettingsVisible = true;
            (NavView.SettingsItem as FrameworkElement)?.Focus(FocusState.Programmatic);
        }
    }
}