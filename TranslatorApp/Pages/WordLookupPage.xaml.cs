using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TranslatorApp.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.UI;
using static TranslatorApp.MainWindow;

namespace TranslatorApp.Pages
{
    // 实现通用接口
    public sealed partial class WordLookupPage : Page, IHasTitleBarControls
    {
        private string _currentQuery = string.Empty;
        private readonly List<string> _history = new();
        public WordLookupPage()
        {
            InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.Loaded += (_, __) => TryBindTitleBarControls();
            Loaded += (_, __) => {
                TryBindTitleBarControls();
                // 模板已应用，强制启动（只在骨架可见时）
                if (CaptionShimmer.Visibility == Visibility.Visible) CaptionShimmer.IsActive = true;
                if (DateShimmer.Visibility == Visibility.Visible) DateShimmer.IsActive = true;
                if (EnShimmer.Visibility == Visibility.Visible) EnShimmer.IsActive = true;
                if (ZhShimmer.Visibility == Visibility.Visible) ZhShimmer.IsActive = true;
                if (ImageShimmer.Visibility == Visibility.Visible) ImageShimmer.IsActive = true;
            };

        }

        private void LoadHistory() => _history.Clear();
        private void SaveHistory() => SettingsService.LookupHistory = new List<string>(_history);

        private void AddToHistory(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return;
            if (!_history.Contains(term))
            {
                _history.Insert(0, term);
                if (_history.Count > 50) _history.RemoveAt(_history.Count - 1);
                SaveHistory();
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var text = sender.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                sender.ItemsSource = null;
                return;
            }
            var suggestions = new List<string>();
            suggestions.AddRange(_history.FindAll(h => h.StartsWith(text, StringComparison.OrdinalIgnoreCase)));
            suggestions.AddRange(new[] { text, $"{text} meaning", $"{text} 翻译", $"{text} 用法" });
            sender.ItemsSource = suggestions;
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var q = args.QueryText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(q)) return;
            _currentQuery = q;
            AddToHistory(q);
            NavigateToSite(q);
        }

        private void NavigateToSite(string query)
        {
            // 关闭每日一句区域与骨架
            ToggleDailySkeleton(false); // 关闭文本与图片骨架
            DailySentenceCard.Visibility = Visibility.Collapsed;

            Web.Visibility = Visibility.Visible;
            WebMask.Visibility = Visibility.Visible;
            FabFavorite.Visibility = Visibility.Collapsed;
            FabFavorite.Visibility = Visibility.Visible;
            var site =
                (App.MainWindow?.LookupSiteComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                ?? SettingsService.LastLookupSite
                ?? "Youdao";

            SettingsService.LastLookupSite = site;

            string url = site switch
            {
                "Bing" => $"https://cn.bing.com/dict/search?q={Uri.EscapeDataString(query)}",
                "Google" => $"https://www.google.com/search?q=define%3A{Uri.EscapeDataString(query)}",
                "Youdao" => $"https://dict.youdao.com/result?word={Uri.EscapeDataString(query)}&lang=en",
                _ => $"https://dict.youdao.com/result?word={Uri.EscapeDataString(query)}&lang=en"
            };

            SafeSetWebSource(url);
        }

        private void FabFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_currentQuery))
            {
                FavoritesService.Add(_currentQuery);
                FavoritesService.Save();
            }
        }
        // 1) 统一的 HttpClient，避免频繁创建导致的 Socket 异常和握手抖动
        private static readonly HttpClient _http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                SslOptions = { /* 保持默认即可；如需忽略证书可在此扩展，但不推荐 */ }
            };
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TranslatorApp/1.0 (+https://github.com/Furry-Xiyi/TranslatorApp)");
            return client;
        }

        // 2) XAML 元素“存活”检查（避免访问已释放的对象）
        private static bool IsElementAlive(FrameworkElement? fe)
        {
            return fe != null && fe.XamlRoot != null && fe.XamlRoot.Content != null;
        }

        // 3) 安全绑定标题栏控件上事件（带二次重试与 ObjectDisposed 兜底）
        private void TryBindTitleBarControls()
        {
            if (App.MainWindow == null) return;

            try
            {
                App.MainWindow.EnsureTitleBarControls();

                var cbSite = App.MainWindow.LookupSiteComboBox;
                var searchBox = App.MainWindow.LookupSearchBox;

                if (cbSite != null && searchBox != null &&
                    IsElementAlive(cbSite) && IsElementAlive(searchBox))
                {
                    cbSite.SelectionChanged -= LookupSite_SelectionChanged;
                    cbSite.SelectionChanged += LookupSite_SelectionChanged;

                    searchBox.TextChanged -= SearchBox_TextChanged;
                    searchBox.TextChanged += SearchBox_TextChanged;
                    searchBox.QuerySubmitted -= SearchBox_QuerySubmitted;
                    searchBox.QuerySubmitted += SearchBox_QuerySubmitted;
                }
                else
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(50);
                        try
                        {
                            if (IsElementAlive(App.MainWindow?.LookupSiteComboBox) &&
                                IsElementAlive(App.MainWindow?.LookupSearchBox))
                            {
                                var cb2 = App.MainWindow!.LookupSiteComboBox!;
                                var sb2 = App.MainWindow!.LookupSearchBox!;

                                cb2.SelectionChanged -= LookupSite_SelectionChanged;
                                cb2.SelectionChanged += LookupSite_SelectionChanged;

                                sb2.TextChanged -= SearchBox_TextChanged;
                                sb2.TextChanged += SearchBox_TextChanged;
                                sb2.QuerySubmitted -= SearchBox_QuerySubmitted;
                                sb2.QuerySubmitted += SearchBox_QuerySubmitted;
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            System.Diagnostics.Debug.WriteLine("[WordLookupPage] 二次绑定时对象已释放，跳过");
                        }
                    });
                }
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debug.WriteLine("[WordLookupPage] 初次绑定时对象已释放，跳过");
            }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 先让初始化流程统一接管骨架/图片/网页等状态，避免竞态
            InitializeStartupNavigation(e.Parameter);

            var app = (App)Application.Current;
            var cached = app.CachedDailySentence;

            if (!string.IsNullOrWhiteSpace(_currentQuery))
            {
                // 从查词模式切回每日一句
                _currentQuery = string.Empty;
                TearDownWebView();

                DailySentenceCard.Visibility = Visibility.Visible;
                FabFavorite.Visibility = Visibility.Collapsed;

                if (cached != null)
                {
                    if (!string.IsNullOrWhiteSpace(cached.Caption)) TxtCaption.Text = cached.Caption;
                    if (!string.IsNullOrWhiteSpace(cached.Date)) TxtDate.Text = cached.Date;
                    if (!string.IsNullOrWhiteSpace(cached.En)) TxtEn.Text = cached.En;
                    if (!string.IsNullOrWhiteSpace(cached.Zh)) TxtZh.Text = cached.Zh;
                }

                // 仅在“刚从查词切回每日一句”场景下兜底恢复图片
                RestoreDailyImageIfNeeded();
            }

            // 注意：不再在“已是每日一句模式”的 else 分支里二次恢复图片/骨架，避免与 InitializeStartupNavigation 打架
        }

        private void RestoreDailyImageIfNeeded()
        {
            if (!string.IsNullOrWhiteSpace(_currentQuery))
                return;

            var app = (App)Application.Current;
            var cached = app.CachedDailySentence;
            if (cached == null) return;

            if (ImgPic.Source != null)
            {
                ImgPic.Visibility = Visibility.Visible;
                ControlPanel.Visibility = Visibility.Visible;
                ImageShimmer.IsActive = false;
                ImageShimmer.Visibility = Visibility.Collapsed;
                return;
            }

            if (!string.IsNullOrEmpty(cached.PicUrl))
            {
                _ = SetImageAsync(cached.PicUrl);
                return;
            }

            ImgPic.Visibility = Visibility.Collapsed;
            ImageShimmer.IsActive = false;
            ImageShimmer.Visibility = Visibility.Collapsed;
        }


        private void LookupSite_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var site = (App.MainWindow?.LookupSiteComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(site))
                SettingsService.LastLookupSite = site;

            var text = App.MainWindow?.LookupSearchBox?.Text;
            if (!string.IsNullOrWhiteSpace(text))
                NavigateToSite(text!);
        }

        private void Web_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            WebMask.Visibility = Visibility.Collapsed;
            if (!string.IsNullOrWhiteSpace(_currentQuery))
                FabFavorite.Visibility = Visibility.Visible;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // 只关闭/清空 WebView2，不动每日一句图片
            TearDownWebView();

            // 解绑事件
            if (App.MainWindow != null)
            {
                var cbSite = App.MainWindow.LookupSiteComboBox;
                var searchBox = App.MainWindow.LookupSearchBox;

                if (cbSite != null)
                    cbSite.SelectionChanged -= LookupSite_SelectionChanged;

                if (searchBox != null)
                {
                    searchBox.TextChanged -= SearchBox_TextChanged;
                    searchBox.QuerySubmitted -= SearchBox_QuerySubmitted;
                }
            }

            var app = (App)Application.Current;
            app.DailySentenceUpdated -= OnDailySentenceUpdated;
        }

        private void TearDownWebView()
        {
            try
            {
                if (Web.CoreWebView2 != null)
                {
                    // 停止当前加载并清空到 about:blank
                    Web.CoreWebView2.Stop();
                    Web.CoreWebView2.Navigate("about:blank");
                }
                else
                {
                    // 尚未初始化 CoreWebView2 时，直接改 Source
                    SafeSetWebSource("about:blank");
                }
            }
            catch { /* 忽略清理过程中的异常 */ }

            // 彻底隐去 UI，并清空 Source 引用
            Web.Source = null;
            WebMask.Visibility = Visibility.Collapsed;
            FabFavorite.Visibility = Visibility.Collapsed;
            Web.Visibility = Visibility.Collapsed;
        }

        private async void InitializeStartupNavigation(object? navParam)
        {
            await EnsureWebReadyAsync();
            System.Diagnostics.Debug.WriteLine("进入 InitializeStartupNavigation");

            if (navParam is string term && !string.IsNullOrWhiteSpace(term))
            {
                ToggleDailySkeleton(false);
                DailySentenceCard.Visibility = Visibility.Collapsed;
                _currentQuery = term;
                AddToHistory(term);
                NavigateToSite(term);
                await WaitForNextNavigationAsync();
                Web.Opacity = 1;
                return;
            }

            var app = (App)Application.Current;
            var cached = app.CachedDailySentence;

            bool cacheUsable = cached != null &&
                               !string.IsNullOrWhiteSpace(cached!.Caption) &&
                               !string.IsNullOrWhiteSpace(cached.En);

            bool hasImageInView = ImgPic.Source != null;
            bool hasPicUrl = !string.IsNullOrEmpty(cached?.PicUrl);

            if (cacheUsable)
            {
                // ✅ 如果已有图片，直接填充文字并收尾骨架，跳过骨架/延迟逻辑，避免闪烁
                if (hasImageInView)
                {
                    TxtCaption.Text = cached!.Caption;
                    TxtCaption.Visibility = Visibility.Visible;
                    TxtDate.Text = cached.Date;
                    TxtDate.Visibility = Visibility.Visible;
                    TxtEn.Text = cached.En;
                    ControlPanel.Visibility = Visibility.Visible;
                    TxtZh.Text = cached.Zh;
                    TxtZh.Visibility = Visibility.Visible;

                    CaptionShimmer.Visibility = Visibility.Collapsed;
                    DateShimmer.Visibility = Visibility.Collapsed;
                    EnShimmer.Visibility = Visibility.Collapsed;
                    ZhShimmer.Visibility = Visibility.Collapsed;
                    ImageShimmer.Visibility = Visibility.Collapsed;

                    Web.Opacity = 1;
                    return;
                }

                // 原逻辑：需要骨架时才开
                bool needImageSkeleton = hasPicUrl && !hasImageInView;
                ToggleDailySkeleton(true, includeImage: needImageSkeleton);
                StartSkeletonAnimation(includeImage: needImageSkeleton);

                await Task.Delay(220);
                await ApplyDailySentenceDataAsync(cached!);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("无缓存，开始网络加载");

                ToggleDailySkeleton(true, includeImage: true);
                StartSkeletonAnimation(includeImage: true);

                DailySentenceData? daily = null;
                try
                {
                    daily = await GetDailySentenceData();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetDailySentenceData 异常: {ex}");
                }

                if (daily != null)
                {
                    System.Diagnostics.Debug.WriteLine("网络加载成功");
                    app.CachedDailySentence = daily;

                    await Task.Delay(800);
                    await ApplyDailySentenceDataAsync(daily);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("网络加载失败，调用兜底收尾");
                    ToggleDailySkeleton(false);
                    DailySentenceCard.Visibility = Visibility.Collapsed;
                }
            }

            Web.Opacity = 1;
        }

        private void StartSkeletonAnimation(bool includeImage)
        {
            _ = DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await Task.Delay(120);
                    CaptionShimmer.IsActive = true;
                    DateShimmer.IsActive = true;
                    EnShimmer.IsActive = true;
                    ZhShimmer.IsActive = true;
                    if (includeImage)
                        ImageShimmer.IsActive = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[WordLookupPage] 启动骨架动画失败: {ex}");
                }
            });
        }

        private async void OnDailySentenceUpdated()
        {
            var app = (App)Application.Current;
            var data = app.CachedDailySentence;
            if (data == null || !string.IsNullOrWhiteSpace(_currentQuery)) return;

            await ApplyDailySentenceDataAsync(data);

            DailySentenceCard.Visibility = Visibility.Visible;
            FabFavorite.Visibility = Visibility.Collapsed;
        }

        private async Task EnsureWebReadyAsync()
        {
            try
            {
                await Web.EnsureCoreWebView2Async();
                if (Web.CoreWebView2 != null)
                {
                    Web.DefaultBackgroundColor = Colors.Transparent;
                    await App.InitWebView2Async(Web.CoreWebView2);
                }
            }
            catch { }
        }

        private async Task WaitForNextNavigationAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            void Handler(object s, CoreWebView2NavigationCompletedEventArgs e)
            {
                try { tcs.TrySetResult(true); } catch { }
                Web.NavigationCompleted -= Handler;
            }
            Web.NavigationCompleted += Handler;
            await Task.WhenAny(tcs.Task, Task.Delay(4000));
        }

        public class DailySentenceData
        {
            public string Caption { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;
            public string En { get; set; } = string.Empty;
            public string Zh { get; set; } = string.Empty;
            public string PicUrl { get; set; } = string.Empty;
            public string TtsUrl { get; set; } = string.Empty;
        }

        private async Task<DailySentenceData?> GetDailySentenceData()
        {
            try
            {
                using var client = new HttpClient();
                var json = await client.GetStringAsync("https://open.iciba.com/dsapi/");
                using var doc = JsonDocument.Parse(json);

                var caption = doc.RootElement.TryGetProperty("caption", out var cap) ? cap.GetString() ?? "" : "";
                var date = doc.RootElement.TryGetProperty("dateline", out var dl) ? dl.GetString() ?? "" : "";
                var en = doc.RootElement.GetProperty("content").GetString() ?? "";
                var zh = doc.RootElement.GetProperty("note").GetString() ?? "";
                var tts = doc.RootElement.TryGetProperty("tts", out var t) ? t.GetString() ?? "" : "";
                var pic =
                    (doc.RootElement.TryGetProperty("picture2", out var p2) ? p2.GetString() : null)
                    ?? (doc.RootElement.TryGetProperty("picture", out var p1) ? p1.GetString() : null)
                    ?? "";

                return new DailySentenceData
                {
                    Caption = caption,
                    Date = date,
                    En = en,
                    Zh = zh,
                    PicUrl = pic,
                    TtsUrl = tts
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task ApplyDailySentenceDataAsync(DailySentenceData data)
        {
            DailySentenceCard.Visibility = Visibility.Visible;

            // ===== 填充文本并收尾对应骨架 =====
            TxtCaption.Text = data.Caption;
            TxtCaption.Visibility = Visibility.Visible;
            CaptionShimmer.IsActive = false;
            CaptionShimmer.Visibility = Visibility.Collapsed;

            TxtDate.Text = data.Date;
            TxtDate.Visibility = Visibility.Visible;
            DateShimmer.IsActive = false;
            DateShimmer.Visibility = Visibility.Collapsed;

            TxtEn.Text = data.En;
            ControlPanel.Visibility = Visibility.Visible;
            EnShimmer.IsActive = false;
            EnShimmer.Visibility = Visibility.Collapsed;

            TxtZh.Text = data.Zh;
            TxtZh.Visibility = Visibility.Visible;
            ZhShimmer.IsActive = false;
            ZhShimmer.Visibility = Visibility.Collapsed;

            // ===== TTS 按钮 =====
            BtnPlayTts.Click -= BtnPlayTts_Click;
            if (!string.IsNullOrEmpty(data.TtsUrl))
                BtnPlayTts.Click += BtnPlayTts_Click;

            // ===== 图片骨架：已有 Source 则不重载，避免闪烁 =====
            if (!string.IsNullOrWhiteSpace(data.PicUrl))
            {
                if (ImgPic.Source != null)
                {
                    // 已有图：直接显图并收尾骨架（不再二次下载）
                    ImgPic.Visibility = Visibility.Visible;
                    ImageShimmer.IsActive = false;
                    ImageShimmer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    await SetImageAsync(data.PicUrl);
                }
            }
            else
            {
                ImageShimmer.IsActive = false;
                ImageShimmer.Visibility = Visibility.Collapsed;
                ImgPic.Visibility = Visibility.Collapsed;
            }

            // ===== 微软商店风格上浮动画 =====
            try
            {
                DailySentenceCard.Opacity = 0;
                DailySentenceCard.RenderTransform = new TranslateTransform { Y = 20 };

                var sb = new Storyboard();

                var fadeAnim = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeAnim, DailySentenceCard);
                Storyboard.SetTargetProperty(fadeAnim, "Opacity");
                sb.Children.Add(fadeAnim);

                var translateAnim = new DoubleAnimation
                {
                    From = 20,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(translateAnim, DailySentenceCard);
                Storyboard.SetTargetProperty(translateAnim, "(UIElement.RenderTransform).(TranslateTransform.Y)");
                sb.Children.Add(translateAnim);

                sb.Begin();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WordLookupPage] 上浮动画失败: {ex}");
            }
        }

        private void BtnPlayTts_Click(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            var data = app.CachedDailySentence;

            if (string.IsNullOrEmpty(_currentQuery) &&
                data != null &&
                !string.IsNullOrEmpty(data.TtsUrl))
            {
                var player = new MediaPlayer
                {
                    Source = MediaSource.CreateFromUri(new Uri(data.TtsUrl))
                };
                player.Play();
            }
        }
        // 统一控制每日一句骨架的显示与动画状态
        // 统一控制每日一句骨架的显示与动画状态
        private void ToggleDailySkeleton(bool isLoading, bool includeImage = true)
        {
            if (isLoading)
            {
                DailySentenceCard.Visibility = Visibility.Visible;

                // 文本骨架：始终在视觉树中
                CaptionShimmer.Visibility = Visibility.Visible;
                CaptionShimmer.IsActive = true;

                DateShimmer.Visibility = Visibility.Visible;
                DateShimmer.IsActive = true;

                EnShimmer.Visibility = Visibility.Visible;
                EnShimmer.IsActive = true;

                ZhShimmer.Visibility = Visibility.Visible;
                ZhShimmer.IsActive = true;

                if (includeImage)
                {
                    // 只有需要图片骨架时才接管图片可见性，避免已有图片被折叠造成闪烁
                    ImageShimmer.Visibility = Visibility.Visible;
                    ImageShimmer.IsActive = true;
                    ImgPic.Visibility = Visibility.Collapsed;
                }

                // 隐藏文本（图片是否隐藏由 includeImage 决定）
                TxtCaption.Visibility = Visibility.Collapsed;
                TxtDate.Visibility = Visibility.Collapsed;
                ControlPanel.Visibility = Visibility.Collapsed;
                TxtZh.Visibility = Visibility.Collapsed;
            }
            else
            {
                CaptionShimmer.IsActive = false;
                CaptionShimmer.Visibility = Visibility.Collapsed;

                DateShimmer.IsActive = false;
                DateShimmer.Visibility = Visibility.Collapsed;

                EnShimmer.IsActive = false;
                EnShimmer.Visibility = Visibility.Collapsed;

                ZhShimmer.IsActive = false;
                ZhShimmer.Visibility = Visibility.Collapsed;

                if (includeImage)
                {
                    // 仅当之前启用了图片骨架时才收尾它；否则不要动当前图片可见性
                    ImageShimmer.IsActive = false;
                    ImageShimmer.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task SetImageAsync(string picUrl)
        {
            ImageShimmer.Visibility = Visibility.Visible;
            ImageShimmer.IsActive = true;
            ImgPic.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(picUrl))
            {
                ImageShimmer.IsActive = false;
                ImageShimmer.Visibility = Visibility.Collapsed;
                ImgPic.Visibility = Visibility.Collapsed;
                return;
            }

            var bmp = new BitmapImage();

            bmp.ImageOpened += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ImgPic.Source = bmp;
                    ImgPic.Visibility = Visibility.Visible;
                    ImageShimmer.IsActive = false;
                    ImageShimmer.Visibility = Visibility.Collapsed;
                });
            };

            bmp.ImageFailed += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ImgPic.Visibility = Visibility.Collapsed;
                    ImageShimmer.IsActive = false;
                    ImageShimmer.Visibility = Visibility.Collapsed;
                });
            };

            try
            {
                if (picUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var comma = picUrl.IndexOf(',');
                    if (comma > 0 && comma < picUrl.Length - 1)
                    {
                        var base64 = picUrl[(comma + 1)..];
                        var bytes = Convert.FromBase64String(base64);

                        using var stream = new InMemoryRandomAccessStream();
                        using (var writer = new DataWriter(stream))
                        {
                            writer.WriteBytes(bytes);
                            await writer.StoreAsync();
                        }
                        stream.Seek(0);
                        await bmp.SetSourceAsync(stream);
                    }
                    else
                    {
                        ImageShimmer.IsActive = false;
                        ImageShimmer.Visibility = Visibility.Collapsed;
                        ImgPic.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    using var resp = await _http.GetAsync(picUrl, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();

                    await using var netStream = await resp.Content.ReadAsStreamAsync();
                    using var mem = new InMemoryRandomAccessStream();
                    await netStream.CopyToAsync(mem.AsStreamForWrite());
                    mem.Seek(0);

                    await bmp.SetSourceAsync(mem);
                }
            }
            catch
            {
                ImgPic.Visibility = Visibility.Collapsed;
                ImageShimmer.IsActive = false;
                ImageShimmer.Visibility = Visibility.Collapsed;
            }
        }

        private void SafeSetWebSource(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                try { Web.Source = uri; }
                catch (ArgumentException) { }
            }
        }
    }
}