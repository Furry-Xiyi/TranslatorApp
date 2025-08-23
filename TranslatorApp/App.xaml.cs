using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TranslatorApp.Pages;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TranslatorApp
{
    public partial class App : Application
    {
        public static MainWindow? MainWindow { get; private set; }

        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        });

        // 有道隐藏规则
        private const string HideCssRules = @"
            header, .top, .top-nav, .top-nav-wrap, .nav, .nav-bar, .nav-wrap, .top-banner,
            .header, .header-light,
            .search-wrapper, .search-area, .search-bar-container, .search-bar-bg,
            header .search, header .search-wrapper, header .search-box,
            .search-box, .search-container, .search-bar-wrap,
            footer, .footer, .yd-footer, .global-footer, .ft, .ft-wrap, .ft-container,
            .footer-light, .light-footer, .m-ft, .m-footer, .footer-wrap,
            [class*='footer'], [id*='footer'], [class*='copyright'], [id*='copyright'] {
                display: none !important;
            }
        ";

        // Bing 隐藏规则（保留分页）
        private const string BingHideCss = @"
            /* 顶部标题/搜索区 */
            #b_header, #sw_hdr, #sb_form, .b_scopebar, .b_logo {
                display: none !important;
            }

            /* 页脚与附加信息（不含分页） */
            #b_footer, .b_footnote, #b_pageFeedback, #b_feedback {
                display: none !important;
            }

            /* 兜底：确保分页可见 */
            .b_pag, nav[aria-label*='Pagination'], nav[role=""navigation""][aria-label*=""页""] {
                display: block !important;
                visibility: visible !important;
            }
        ";

        private const string YoudaoDarkCss = @"
            :root{ color-scheme: dark; }
            html, body{
              background:#0f0f0f !important;
              color:#ddd !important;
            }
            a{ color:#6fb1ff !important; }
        ";

        private static bool isClosing = false;

        public App()
        {
            InitializeComponent();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            ApplySavedTheme();
            ApplySavedBackdrop();
            MainWindow.Activate();
            MainWindow.Closed += (s, e) => { isClosing = true; };
        }

        private void ApplySavedTheme()
        {
            var themeValue = ApplicationData.Current.LocalSettings.Values["AppTheme"] as string ?? "Default";
            var theme = themeValue switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            if (MainWindow?.Content is FrameworkElement fe)
                fe.RequestedTheme = theme;
        }

        private void ApplySavedBackdrop()
        {
            var tag = ApplicationData.Current.LocalSettings.Values["BackdropMaterial"] as string ?? "Mica";
            try
            {
                MainWindow!.SystemBackdrop = tag switch
                {
                    "None" => null,
                    "MicaAlt" => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                    "Acrylic" => new DesktopAcrylicBackdrop(),
                    _ => new MicaBackdrop { Kind = MicaKind.Base }
                };
            }
            catch
            {
                MainWindow!.SystemBackdrop = null;
            }
        }

        public static void StopWebView2Intercept() => isClosing = true;

        public static async Task InitWebView2Async(CoreWebView2 core)
        {
            if (core == null) return;

            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Document);
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Stylesheet);

            // 有道 HTML 拦截
            core.WebResourceRequested += async (s, e) =>
            {
                if (isClosing || core?.Environment == null || e.ResourceContext != CoreWebView2WebResourceContext.Document) return;
                if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) ||
                    !uri.Host.EndsWith("youdao.com", StringComparison.OrdinalIgnoreCase)) return;

                var deferral = e.GetDeferral();
                try
                {
                    string html;
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                        html = await _http.GetStringAsync(e.Request.Uri, cts.Token);
                    }
                    catch { deferral.Complete(); return; }

                    int headIndex = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
                    if (headIndex >= 0)
                    {
                        int closeHeadTag = html.IndexOf(">", headIndex);
                        if (closeHeadTag > 0)
                        {
                            bool isDark = MainWindow?.Content is FrameworkElement fe && fe.ActualTheme == ElementTheme.Dark;
                            string styleTag = isDark
                                ? $"<style>{HideCssRules}{YoudaoDarkCss}</style>"
                                : $"<style>{HideCssRules}</style>";
                            html = html.Insert(closeHeadTag + 1, styleTag);
                        }
                    }

                    var ras = new InMemoryRandomAccessStream();
                    var writer = new DataWriter(ras)
                    {
                        UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8,
                        ByteOrder = ByteOrder.LittleEndian
                    };
                    writer.WriteBytes(Encoding.UTF8.GetBytes(html));
                    await writer.StoreAsync();
                    ras.Seek(0);
                    e.Response = core.Environment.CreateWebResourceResponse(ras, 200, "OK", "Content-Type: text/html; charset=utf-8");
                }
                finally { deferral.Complete(); }
            };

            // 有道 CSS 拦截
            core.WebResourceRequested += async (s, e) =>
            {
                if (isClosing || core?.Environment == null || e.ResourceContext != CoreWebView2WebResourceContext.Stylesheet) return;
                if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) ||
                    !uri.Host.EndsWith("youdao.com", StringComparison.OrdinalIgnoreCase)) return;

                var deferral = e.GetDeferral();
                try
                {
                    string cssText;
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                        cssText = await _http.GetStringAsync(e.Request.Uri, cts.Token);
                    }
                    catch { deferral.Complete(); return; }

                    cssText += "\n" + HideCssRules;

                    var ras = new InMemoryRandomAccessStream();
                    var writer = new DataWriter(ras)
                    {
                        UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8,
                        ByteOrder = ByteOrder.LittleEndian
                    };
                    writer.WriteBytes(Encoding.UTF8.GetBytes(cssText));
                    await writer.StoreAsync();
                    ras.Seek(0);
                    e.Response = core.Environment.CreateWebResourceResponse(ras, 200, "OK", "Content-Type: text/css; charset=utf-8");
                }
                finally { deferral.Complete(); }
            };

            // Bing HTML 拦截
            core.WebResourceRequested += async (s, e) =>
            {
            if (isClosing || core?.Environment == null || e.ResourceContext != CoreWebView2WebResourceContext.Document) return;
            if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) ||
                !uri.Host.EndsWith("bing.com", StringComparison.OrdinalIgnoreCase)) return;

            var deferral = e.GetDeferral();
            try
            {
                string html;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    html = await _http.GetStringAsync(e.Request.Uri, cts.Token);
                }
                catch { deferral.Complete(); return; }

                int headIndex = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
                if (headIndex >= 0)
                {
                    int closeHeadTag = html.IndexOf(">", headIndex);
                    if (closeHeadTag > 0)
                    {
                        string styleTag = $"<style>{BingHideCss}</style>";
                        html = html.Insert(closeHeadTag + 1, styleTag);
                    }
                }

                var ras = new InMemoryRandomAccessStream();
                    var writer = new DataWriter(ras)
                    {
                        UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8,
                        ByteOrder = ByteOrder.LittleEndian
                    };
                    writer.WriteBytes(Encoding.UTF8.GetBytes(html));
                    await writer.StoreAsync();
                    ras.Seek(0);
                    e.Response = core.Environment.CreateWebResourceResponse(
                        ras, 200, "OK", "Content-Type: text/html; charset=utf-8");
                }
                finally { deferral.Complete(); }
            };

            // Bing CSS 拦截
            core.WebResourceRequested += async (s, e) =>
            {
                if (isClosing || core?.Environment == null || e.ResourceContext != CoreWebView2WebResourceContext.Stylesheet) return;
                if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) ||
                    !uri.Host.EndsWith("bing.com", StringComparison.OrdinalIgnoreCase)) return;

                var deferral = e.GetDeferral();
                try
                {
                    string cssText;
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                        cssText = await _http.GetStringAsync(e.Request.Uri, cts.Token);
                    }
                    catch { deferral.Complete(); return; }

                    cssText += "\n" + BingHideCss;

                    var ras = new InMemoryRandomAccessStream();
                    var writer = new DataWriter(ras)
                    {
                        UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8,
                        ByteOrder = ByteOrder.LittleEndian
                    };
                    writer.WriteBytes(Encoding.UTF8.GetBytes(cssText));
                    await writer.StoreAsync();
                    ras.Seek(0);
                    e.Response = core.Environment.CreateWebResourceResponse(
                        ras, 200, "OK", "Content-Type: text/css; charset=utf-8");
                }
                finally { deferral.Complete(); }
            };

            // 设置 UA 和主题
            bool isDarkTheme = MainWindow?.Content is FrameworkElement fe2 && fe2.ActualTheme == ElementTheme.Dark;
            try
            {
                core.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/124.0.0.0 Safari/537.36 Edg/124.0.0.0";

                core.Profile.PreferredColorScheme = isDarkTheme
                    ? CoreWebView2PreferredColorScheme.Dark
                    : CoreWebView2PreferredColorScheme.Light;
            }
            catch { }
        }
    }
}
