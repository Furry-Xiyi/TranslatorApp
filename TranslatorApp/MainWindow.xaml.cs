using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;
using TranslatorApp.Pages;
using TranslatorApp.Services;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace TranslatorApp;

public sealed partial class MainWindow : Window
{
    private bool _activatedOnce = false;
    private bool _welcomeShown = false;
    private AppWindow? _appWindow;

    public Panel TitleBarCenterPanel => TitleBarCenterHost;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(CustomDragRegion);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow is not null)
        {
            var titleBar = _appWindow.TitleBar;
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var transparent = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
                titleBar.ButtonBackgroundColor = transparent;
                titleBar.ButtonInactiveBackgroundColor = transparent;
            }
            UpdateDragRegionPadding();
            _appWindow.Changed += AppWindow_Changed;
        }

        // 按钮前景色跟随系统强调色
        var uiSettings = new UISettings();
        uiSettings.ColorValuesChanged += (s, e) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_appWindow is not null)
                {
                    var accent = uiSettings.GetColorValue(UIColorType.Accent);
                    _appWindow.TitleBar.ButtonForegroundColor = accent;
                }
            });
        };

        SizeChanged += (_, __) => UpdateDragRegionPadding();
        TryLoadAppIcon();

        NavView.SelectedItem = Nav_Online;
        NavigateTo(typeof(OnlineTranslatePage));

        ContentFrame.Navigated += ContentFrame_Navigated;
        Activated += MainWindow_Activated;
    }

    public void ShowLoadingOverlay() => LoadingOverlay.Visibility = Visibility.Visible;
    public void HideLoadingOverlay() => LoadingOverlay.Visibility = Visibility.Collapsed;

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_appWindow is null || CustomDragRegion is null) return;
        DispatcherQueue.TryEnqueue(UpdateDragRegionPadding);
    }

    private void UpdateDragRegionPadding()
    {
        if (_appWindow is null || CustomDragRegion is null) return;
        var tb = _appWindow.TitleBar;
        if (tb != null)
        {
            CustomDragRegion.Padding = new Thickness(tb.LeftInset, 0, tb.RightInset, 0);
        }
    }

    private void TryLoadAppIcon()
    {
        try
        {
            var uri = Package.Current?.Logo;
            if (uri != null)
            {
                AppIconImage.Source = new BitmapImage(uri);
                return;
            }
        }
        catch { }

        AppIconImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/AppIcon.png"));
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_activatedOnce) return;
        _activatedOnce = true;

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
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        NavView.IsBackEnabled = ContentFrame.CanGoBack;

        if (e.SourcePageType == typeof(SettingsPage))
        {
            NavView.SelectedItem = NavView.SettingsItem;
            return;
        }

        var match = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => (string)item.Tag == e.SourcePageType.Name);

        if (match != null)
        {
            NavView.SelectedItem = match;
        }
    }

    private async Task EnsureXamlRootAsync()
    {
        if (NavView.XamlRoot != null) return;

        var tcs = new TaskCompletionSource<object?>();
        RoutedEventHandler? handler = null;
        handler = (s, e) =>
        {
            NavView.Loaded -= handler;
            tcs.TrySetResult(null);
        };
        NavView.Loaded += handler;

        await tcs.Task;
    }

    private async Task ShowWelcomeDialogAsync()
    {
        await EnsureXamlRootAsync();

        var dialog = new ContentDialog
        {
            XamlRoot = NavView.XamlRoot,
            Title = "欢迎使用翻译",
            PrimaryButtonText = "去填写",
            CloseButtonText = "稍后",
            DefaultButton = ContentDialogButton.Primary,
            Content = new TextBlock
            {
                Text = "使用翻译需要填写 API 密钥",
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap
            }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            NavigateTo(typeof(SettingsPage));
            NavView.IsSettingsVisible = true;
            (NavView.SettingsItem as FrameworkElement)?.Focus(FocusState.Programmatic);
        }
    }
}