using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
using Windows.ApplicationModel.Resources;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace TranslatorApp
{
    public sealed partial class MainWindow : Window
    {
        private bool _activatedOnce = false;
        private bool _welcomeShown = false;
        private readonly AppWindow? _appWindow;
        // 新增：双面板引用与收藏页搜索框
        private FrameworkElement? _lookupTitleBarContent;
        private FrameworkElement? _favoritesTitleBarContent;

        public AutoSuggestBox? FavoritesSearchBox { get; private set; }
        private FrameworkElement? _titleBarContent;
        private bool _pendingDragUpdate = false;
        private const string Key_WhatsNewShownVersion = "WhatsNewShownVersion";
        private const string Key_UpdateIgnoreVersion = "UpdateIgnoreVersion";

        private const string GitHubOwner = "Furry-Xiyi";
        private const string GitHubRepo = "TranslatorApp";

        public Panel TitleBarCenterPanel => TitleBarCenterHost;

        // 持久化查词控件
        public ComboBox? LookupSiteComboBox { get; private set; }
        public AutoSuggestBox? LookupSearchBox { get; private set; }
        public interface IHasTitleBarControls { }
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

            SizeChanged += (_, __) =>
            {
                UpdateDragRegionPadding();
                UpdateTitleBarDragRegions();
            };

            TryLoadAppIcon();
            InsertWhatsNewNavItem();

            ContentFrame.Navigated += ContentFrame_Navigated;

            RootGrid.Loaded += (_, __) =>
            {
                TryAttachTitleBarForCurrentPage();
            };

            NavView.SelectedItem = Nav_Online;
            NavigateTo(typeof(WordLookupPage));

            Activated += MainWindow_Activated;
        }
        private void TryAttachTitleBarForCurrentPage()
        {
            if (_appWindow == null || RootGrid?.XamlRoot == null)
                return;

            FrameworkElement? targetContent = null;

            if (ContentFrame.Content is Pages.WordLookupPage)
            {
                EnsureTitleBarControls();
                targetContent = _lookupTitleBarContent as FrameworkElement ?? _titleBarContent;
            }
            else if (ContentFrame.Content is Pages.FavoritesPage)
            {
                EnsureFavoritesTitleBarControls();
                targetContent = _favoritesTitleBarContent as FrameworkElement;
            }

            // 先把旧 content 的事件解绑
            if (_titleBarContent != null)
            {
                _titleBarContent.SizeChanged -= TitleBarContent_SizeChanged;
                _titleBarContent.LayoutUpdated -= TitleBarContent_LayoutUpdated;
            }
            if (RootGrid?.XamlRoot != null)
            {
                RootGrid.XamlRoot.Changed -= XamlRoot_Changed;
            }

            if (targetContent != null)
            {
                if (_lookupTitleBarContent != null && _lookupTitleBarContent != targetContent)
                    _lookupTitleBarContent.Visibility = Visibility.Collapsed;
                if (_favoritesTitleBarContent != null && _favoritesTitleBarContent != targetContent)
                    _favoritesTitleBarContent.Visibility = Visibility.Collapsed;

                targetContent.Visibility = Visibility.Visible;
                _titleBarContent = targetContent;

                // 绑定实例级事件处理，避免局部委托导致的解绑失败和 nullability 警告
                _titleBarContent.SizeChanged += TitleBarContent_SizeChanged;
                _titleBarContent.LayoutUpdated += TitleBarContent_LayoutUpdated;

                if (RootGrid?.XamlRoot != null)
                    RootGrid.XamlRoot.Changed += XamlRoot_Changed;

                // 低优先级刷新，避免布局中途取值
                RequestUpdateTitleBarDragRegions();
            }
            else
            {
                if (_titleBarContent != null)
                    _titleBarContent.Visibility = Visibility.Collapsed;

                _titleBarContent = null;
                SetFullDragRectangles();
            }
        }
        private void TitleBarContent_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            RequestUpdateTitleBarDragRegions();
        }

        private void TitleBarContent_LayoutUpdated(object? sender, object e)
        {
            RequestUpdateTitleBarDragRegions();
        }

        private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            RequestUpdateTitleBarDragRegions();
        }
        private void RequestUpdateTitleBarDragRegions()
        {
            DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                UpdateTitleBarDragRegions);
        }
        public void SetTitleBarDragExclusion(FrameworkElement exclude)
        {
            if (_appWindow == null || exclude == null) return;
            if (exclude.XamlRoot?.Content == null) return;

            var titleBar = _appWindow.TitleBar;
            if (titleBar == null) return;

            try
            {
                double scale = exclude.XamlRoot.RasterizationScale;
                double widthDip = Bounds.Width;
                int heightPx = titleBar.Height;
                if (widthDip <= 0 || heightPx <= 0) return;

                GeneralTransform t = exclude.TransformToVisual(null);
                Point origin = t.TransformPoint(new Point(0, 0));
                double leftDip = Math.Max(0, origin.X);
                double centerWidthDip = Math.Max(0, exclude.ActualWidth);
                double rightStartDip = Math.Max(0, leftDip + centerWidthDip);

                int ToPx(double v) => (int)Math.Round(v * scale);

                int leftInsetPx = titleBar.LeftInset;
                int rightInsetPx = titleBar.RightInset;

                int leftX = leftInsetPx;
                int leftW = Math.Max(0, ToPx(leftDip) - leftInsetPx);
                int rightX = ToPx(rightStartDip);
                int rightW = Math.Max(0, ToPx(widthDip) - rightInsetPx - rightX);

                var rects = new RectInt32[]
                {
            new RectInt32(leftX, 0, leftW, heightPx),
            new RectInt32(rightX, 0, rightW, heightPx)
                };

                titleBar.SetDragRectangles(rects);
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debug.WriteLine("[TitleBar] Exclusion 目标已释放，跳过");
            }
            catch
            {
                // 忽略其他偶发异常
            }
        }

        private void SafeUpdateTitleBarLayout()
        {
            if (TitleBarCenterPanel == null || TitleBarCenterPanel.XamlRoot == null) return;
            if (TitleBarCenterPanel.XamlRoot.Content == null) return;
            TitleBarCenterPanel.UpdateLayout();
            UpdateTitleBarDragRegions();
        }

        public void ShowLoadingOverlay() => LoadingOverlay.Visibility = Visibility.Visible;
        public void HideLoadingOverlay() => LoadingOverlay.Visibility = Visibility.Collapsed;

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SafeUpdateTitleBarLayout();
            });
        }

        private void UpdateDragRegionPadding()
        {
            if (_appWindow == null || CustomDragRegion == null) return;
            var tb = _appWindow.TitleBar;
            if (tb != null)
            {
                CustomDragRegion.Padding = new Thickness(tb.LeftInset, 0, tb.RightInset, 0);
            }
        }
        public void UpdateTitleBarDragRegions()
        {
            if (_pendingDragUpdate) return;
            _pendingDragUpdate = true;

            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _pendingDragUpdate = false;
                if (_appWindow == null) return;

                var tb = _appWindow.TitleBar;
                if (tb == null) return;

                // 存活检查
                if (_titleBarContent == null ||
                    _titleBarContent.Visibility != Visibility.Visible ||
                    _titleBarContent.XamlRoot?.Content == null ||
                    _titleBarContent.ActualWidth < 1)
                {
                    SetFullDragRectangles();
                    return;
                }

                try
                {
                    double scale = _titleBarContent.XamlRoot.RasterizationScale;
                    int windowWidthPx = (int)Math.Round(Bounds.Width * scale);
                    int barHeightPx = tb.Height;
                    int leftInsetPx = tb.LeftInset;
                    int rightInsetPx = tb.RightInset;

                    GeneralTransform t = _titleBarContent.TransformToVisual(null);
                    Point origin = t.TransformPoint(new Point(0, 0));
                    double leftDip = Math.Max(0, origin.X);
                    double rightStartDip = Math.Max(0, leftDip + _titleBarContent.ActualWidth);

                    int ToPx(double v) => (int)Math.Round(v * scale);
                    int contentLeftPx = Math.Clamp(ToPx(leftDip), 0, windowWidthPx);
                    int contentRightPx = Math.Clamp(ToPx(rightStartDip), 0, windowWidthPx);

                    if (contentRightPx - contentLeftPx >= windowWidthPx - (leftInsetPx + rightInsetPx) - 2)
                    {
                        DispatcherQueue.TryEnqueue(UpdateTitleBarDragRegions);
                        return;
                    }

                    int leftX = leftInsetPx;
                    int leftW = Math.Max(0, contentLeftPx - leftInsetPx);
                    int rightX = contentRightPx;
                    int rightW = Math.Max(0, windowWidthPx - rightInsetPx - rightX);

                    var rects = new System.Collections.Generic.List<RectInt32>();
                    if (leftW > 0) rects.Add(new RectInt32(leftX, 0, leftW, barHeightPx));
                    if (rightW > 0) rects.Add(new RectInt32(rightX, 0, rightW, barHeightPx));

                    tb.SetDragRectangles(rects.ToArray());
                }
                catch (ObjectDisposedException)
                {
                    System.Diagnostics.Debug.WriteLine("[TitleBar] 元素已释放，跳过更新");
                }
            });
        }

        private void SetFullDragRectangles()
        {
            if (_appWindow == null) return;
            var tb = _appWindow.TitleBar;
            if (tb == null) return;

            double scale = RootGrid?.XamlRoot?.RasterizationScale ?? 1.0;
            int windowWidthPx = (int)Math.Round(Bounds.Width * scale);
            int barHeightPx = tb.Height;
            int leftInsetPx = tb.LeftInset;
            int rightInsetPx = tb.RightInset;

            int x = leftInsetPx;
            int w = Math.Max(0, windowWidthPx - leftInsetPx - rightInsetPx);
            if (w <= 0) return;

            try { tb.SetDragRectangles(new[] { new RectInt32(x, 0, w, barHeightPx) }); } catch { }
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

            _ = CheckForUpdatesAsync();
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
                    case "WordLookupPage":
                        NavigateTo(typeof(WordLookupPage));
                        break;
                    case "OnlineTranslatePage":
                        NavigateTo(typeof(OnlineTranslatePage));
                        break;
                    case "FavoritesPage":
                        NavigateTo(typeof(FavoritesPage));
                        break;
                    case "WhatsNew":
                        _ = ShowWhatsNewDialogAsync(forceOpen: true);
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
                NavView.SelectedItem = NavView.SettingsItem;
            else
                NavView.SelectedItem = NavView.MenuItems
                    .OfType<NavigationViewItem>()
                    .FirstOrDefault(item => (string)item.Tag == e.SourcePageType.Name);

            TryAttachTitleBarForCurrentPage();
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
                Text = "使用互译需要填写 API 密钥",
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

            if (result == ContentDialogResult.Primary)
            {
                WriteLocalSetting(Key_WhatsNewShownVersion, currentVersion);
            }

            return result;
        }

        private Task<string> LoadWhatsNewTextAsync()
        {
            try
            {
                var loader = new ResourceLoader();
                string notes = loader.GetString("ReleaseNotes");

                if (!string.IsNullOrWhiteSpace(notes))
                    return Task.FromResult(notes);

                return Task.FromResult("暂无发行说明");
            }
            catch
            {
                return Task.FromResult("暂无发行说明");
            }
        }

        private void InsertWhatsNewNavItem()
        {
            var item = new NavigationViewItem
            {
                Content = "更新内容",
                Tag = "WhatsNew",
                Icon = new FontIcon { Glyph = "\uE781" }
            };

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

        private async Task CheckForUpdatesAsync()
        {
            if (string.IsNullOrWhiteSpace(GitHubOwner) || string.IsNullOrWhiteSpace(GitHubRepo) ||
                GitHubOwner == "yourname" || GitHubRepo == "TranslatorApp")
            {
                return;
            }

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TranslatorApp-Updater");

                var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                var json = await http.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var tag = root.GetProperty("tag_name").GetString() ?? "";
                var htmlUrl = root.GetProperty("html_url").GetString() ?? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";

                var ignored = ReadLocalSetting<string>(Key_UpdateIgnoreVersion);
                if (!string.IsNullOrEmpty(ignored) && string.Equals(ignored, tag, StringComparison.OrdinalIgnoreCase))
                    return;

                var latest = NormalizeVersion(tag);
                var current = NormalizeVersion(GetCurrentVersionString());

                if (latest > current)
                {
                    await ShowUpdateAvailableDialogAsync(tag, htmlUrl);
                }
            }
            catch
            {
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
                WriteLocalSetting(Key_UpdateIgnoreVersion, tagName);
            }
        }

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

        public void EnsureTitleBarControls()
        {
            // 已创建：刷新并返回
            if (_lookupTitleBarContent is FrameworkElement existing &&
                TitleBarCenterPanel.Children.Contains(existing))
            {
                existing.Visibility = Visibility.Visible;
                SafeUpdateTitleBarLayout();
                return;
            }

            // 首次创建查词栏
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            LookupSiteComboBox = new ComboBox { Width = 140 };
            LookupSiteComboBox.Items.Add(new ComboBoxItem { Content = "Google", Tag = "Google" });
            LookupSiteComboBox.Items.Add(new ComboBoxItem { Content = "Bing", Tag = "Bing" });
            LookupSiteComboBox.Items.Add(new ComboBoxItem { Content = "Youdao", Tag = "Youdao" });

            var lastSite = SettingsService.LastLookupSite;
            foreach (ComboBoxItem item in LookupSiteComboBox.Items)
            {
                if ((item.Tag?.ToString() ?? "") == lastSite)
                {
                    LookupSiteComboBox.SelectedItem = item;
                    break;
                }
            }
            if (LookupSiteComboBox.SelectedItem == null) LookupSiteComboBox.SelectedIndex = 2;

            LookupSearchBox = new AutoSuggestBox
            {
                Width = 600,
                PlaceholderText = "输入要查的词汇...",
                QueryIcon = new SymbolIcon(Symbol.Find)
            };

            panel.Children.Add(LookupSiteComboBox);
            panel.Children.Add(LookupSearchBox);

            if (!TitleBarCenterPanel.Children.Contains(panel))
                TitleBarCenterPanel.Children.Add(panel);

            _lookupTitleBarContent = panel;
            SafeUpdateTitleBarLayout();
        }
        private void EnsureFavoritesTitleBarControls()
        {
            if (_favoritesTitleBarContent is FrameworkElement existing &&
                TitleBarCenterPanel.Children.Contains(existing))
            {
                existing.Visibility = Visibility.Visible;
                SafeUpdateTitleBarLayout();
                return;
            }

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            FavoritesSearchBox = new AutoSuggestBox
            {
                Width = 600,
                PlaceholderText = "搜索收藏的词汇",
                QueryIcon = new SymbolIcon(Symbol.Find)
            };

            // 事件绑定：转发到当前收藏页实例
            FavoritesSearchBox.TextChanged += FavoritesSearchBox_TextChanged;
            FavoritesSearchBox.QuerySubmitted += FavoritesSearchBox_QuerySubmitted;

            panel.Children.Add(FavoritesSearchBox);

            if (!TitleBarCenterPanel.Children.Contains(panel))
                TitleBarCenterPanel.Children.Add(panel);

            _favoritesTitleBarContent = panel;
            SafeUpdateTitleBarLayout();
        }
        private void FavoritesSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            if (ContentFrame.Content is Pages.FavoritesPage favPage)
            {
                favPage.OnFavoritesSearchTextChanged(sender.Text);
            }
        }

        private void FavoritesSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (ContentFrame.Content is Pages.FavoritesPage favPage)
            {
                var text = args.QueryText ?? sender.Text;
                favPage.OnFavoritesSearchQuerySubmitted(text);
            }
        }
        public void SetTitleBarContent(UIElement? content)
        {
            TitleBarCenterPanel.Children.Clear();
            if (content != null)
            {
                TitleBarCenterPanel.Children.Add(content);
                TitleBarCenterPanel.Visibility = Visibility.Visible;
            }
            else
            {
                TitleBarCenterPanel.Visibility = Visibility.Collapsed;
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