using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TranslatorApp.Pages;
using TranslatorApp.Services;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;
using Windows.UI.ViewManagement;

namespace TranslatorApp
{
    public sealed partial class MainWindow : Window
    {
        private bool _activatedOnce = false;
        private bool _welcomeShown = false;
        private readonly AppWindow? _appWindow;

        // 本地设置键
        private const string Key_WhatsNewShownVersion = "WhatsNewShownVersion";
        private const string Key_UpdateIgnoreVersion = "UpdateIgnoreVersion";

        // 你的 GitHub 仓库（用于检查最新版本）
        // TODO: 替换为你的真实仓库。例如 owner = "yourname", repo = "TranslatorApp"
        private const string GitHubOwner = "Furry-Xiyi";
        private const string GitHubRepo = "TranslatorApp";

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

            // 在设置项上方插入“更新内容”按钮（礼炮图标）
            InsertWhatsNewNavItem();

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

            // 激活时的顺序：
            // 1) 若需显示“更新内容”，则先显示；仅当用户点“确定”后，再显示“去填写 API”对话框
            // 2) 若不需要显示“更新内容”，且没有 API Key，则直接显示“去填写 API”
            // 3) 完成后，检查 GitHub 更新（若配置了仓库）
            if (ShouldShowWhatsNewForCurrentVersion())
            {
                var result = await ShowWhatsNewDialogAsync();
                if (result == ContentDialogResult.Primary && !_welcomeShown && !SettingsService.HasAnyApiKey())
                {
                    _welcomeShown = true;
                    await ShowWelcomeDialogAsync();
                }
            }
            else
            {
                if (!_welcomeShown && !SettingsService.HasAnyApiKey())
                {
                    _welcomeShown = true;
                    await ShowWelcomeDialogAsync();
                }
            }

            _ = CheckForUpdatesAsync(); // 后台检查更新
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
                    case "WhatsNew":
                        // 弹出“更新内容”窗口（强制打开，不受是否已看过限制）
                        _ = ShowWhatsNewDialogAsync(forceOpen: true);
                        // 还原选中到当前页面对应的菜单项
                        ReselectCurrentNavItem();
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

            double scale = NavView.XamlRoot.RasterizationScale;

            var contentPanel = new StackPanel
            {
                Spacing = 12 * scale,
                Padding = new Thickness(12 * scale, 0, 12 * scale, 0)
            };

            contentPanel.Children.Add(new TextBlock
            {
                Text = "使用翻译需要填写 API 密钥",
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap
            });

            var template = (DataTemplate)RootGrid.Resources["WelcomeApiExpanderTemplate"];
            var expander = (FrameworkElement)template.LoadContent();
            contentPanel.Children.Add(expander);

            var dialog = new ContentDialog
            {
                XamlRoot = NavView.XamlRoot,
                Title = "欢迎使用翻译",
                PrimaryButtonText = "去填写",
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary,
            };

            dialog.Resources["ContentDialogMinWidth"] = 560.0 * scale;
            dialog.Resources["ContentDialogMaxWidth"] = 720.0 * scale;
            dialog.Content = contentPanel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                NavigateTo(typeof(SettingsPage));
                NavView.IsSettingsVisible = true;
                (NavView.SettingsItem as FrameworkElement)?.Focus(FocusState.Programmatic);
            }
        }

        // ============ 新增：更新内容（What's New） ============

        private bool ShouldShowWhatsNewForCurrentVersion()
        {
            var current = GetCurrentVersionString();
            var lastShown = ReadLocalSetting<string>(Key_WhatsNewShownVersion);
            return string.IsNullOrEmpty(lastShown) || !string.Equals(current, lastShown, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<ContentDialogResult> ShowWhatsNewDialogAsync(bool forceOpen = false)
        {
            await EnsureXamlRootAsync();

            string notes = await LoadWhatsNewTextAsync();
            var currentVersion = GetCurrentVersionString();

            var dialog = new ContentDialog
            {
                XamlRoot = NavView.XamlRoot,
                Title = $"更新内容（{currentVersion}）",
                PrimaryButtonText = "确定",
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = notes,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            var result = await dialog.ShowAsync();

            // 只有用户点击“确定”才记为已读（满足你的“点了确认以后就不再弹出”）
            if (result == ContentDialogResult.Primary)
            {
                WriteLocalSetting(Key_WhatsNewShownVersion, currentVersion);
            }

            return result;
        }

        private async Task<string> LoadWhatsNewTextAsync()
        {
            try
            {
                // 优先从 Assets/WhatsNew.md 读取（可用 Markdown/纯文本）
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/WhatsNew.md"));
                var text = await FileIO.ReadTextAsync(file);
                return string.IsNullOrWhiteSpace(text) ? "暂无更新说明。" : text;
            }
            catch
            {
                return "• 新增：截图 OCR 与语音输入\n• 优化：界面体验与稳定性\n• 修复：若干已知问题";
            }
        }

        private void InsertWhatsNewNavItem()
        {
            // 在 Settings 上方加入一个“更新内容”按钮（礼炮/庆祝类图标）
            var item = new NavigationViewItem
            {
                Content = "更新内容",
                Tag = "WhatsNew",
                Icon = new FontIcon { Glyph = "\uE7E7" } // TODO: 可替换为你喜欢的 MDL2 Glyph（礼炮/庆祝）
            };

            // 插入到菜单末尾（Settings 在底部，会自然位于其上方）
            NavView.FooterMenuItems.Insert(0, item);
        }

        private void ReselectCurrentNavItem()
        {
            if (ContentFrame.CurrentSourcePageType == typeof(SettingsPage))
            {
                NavView.SelectedItem = NavView.SettingsItem;
                return;
            }

            var match = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(mi => (string)mi.Tag == ContentFrame.CurrentSourcePageType.Name);

            if (match != null)
                NavView.SelectedItem = match;
        }

        // ============ 新增：GitHub 更新检查 ============

        private async Task CheckForUpdatesAsync()
        {
            // 未配置仓库则不检查
            if (string.IsNullOrWhiteSpace(GitHubOwner) || string.IsNullOrWhiteSpace(GitHubRepo) ||
                GitHubOwner == "yourname" || GitHubRepo == "TranslatorApp")
            {
                return;
            }

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TranslatorApp-Updater"); // GitHub 需要 UA

                var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                var json = await http.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var tag = root.GetProperty("tag_name").GetString() ?? "";
                var htmlUrl = root.GetProperty("html_url").GetString() ?? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";

                // “暂不”忽略的版本
                var ignored = ReadLocalSetting<string>(Key_UpdateIgnoreVersion);
                if (!string.IsNullOrEmpty(ignored) && string.Equals(ignored, tag, StringComparison.OrdinalIgnoreCase))
                    return;

                // 比较版本（tag 可能以 v 开头）
                var latest = NormalizeVersion(tag);
                var current = NormalizeVersion(GetCurrentVersionString());

                if (latest > current)
                {
                    await ShowUpdateAvailableDialogAsync(tag, htmlUrl);
                }
            }
            catch
            {
                // 静默失败，不打扰用户
            }
        }

        private async Task ShowUpdateAvailableDialogAsync(string tagName, string htmlUrl)
        {
            await EnsureXamlRootAsync();

            var dialog = new ContentDialog
            {
                XamlRoot = NavView.XamlRoot,
                Title = $"发现新版本 {tagName}",
                Content = new TextBlock
                {
                    Text = "有新的更新发布，是否前往下载？",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "去更新",
                CloseButtonText = "暂不",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _ = Launcher.LaunchUriAsync(new Uri(htmlUrl));
            }
            else
            {
                // 暂不：记住这个版本号，不再提示
                WriteLocalSetting(Key_UpdateIgnoreVersion, tagName);
            }
        }

        // ============ 工具方法 ============

        private static string GetCurrentVersionString()
        {
            var v = Package.Current?.Id?.Version;
            if (v.HasValue)
            {
                var ver = v.Value;
                return $"{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";
            }
            return "版本号获取失败";
        }

        private static Version NormalizeVersion(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return new Version(0, 0, 0, 0);
            v = v.Trim();
            if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                v = v[1..];

            // 尝试补全到 4 段
            var parts = v.Split('.');
            while (parts.Length < 4) v += ".0";
            try
            {
                return new Version(v);
            }
            catch
            {
                return new Version(0, 0, 0, 0);
            }
        }

        private static T? ReadLocalSetting<T>(string key)
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            if (values.TryGetValue(key, out var obj) && obj is T t) return t;
            return default;
        }

        private static void WriteLocalSetting(string key, object value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }
    }
}