using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TranslatorApp.Pages;
using Windows.Storage;
using Windows.Storage.Streams;
using static TranslatorApp.Pages.WordLookupPage;

namespace TranslatorApp
{
    public partial class App : Application
    {
        public static MainWindow? MainWindow { get; private set; }

        // ===== 新增：每日一句缓存 =====
        public static string? DailySentenceHtml { get; private set; }

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
/* 顶部大块隐藏 */
#b_header, #sw_hdr, .b_scopebar, .b_logo {
  display: none !important;
}

/* 保留 sb_form，但移出视口+不可见，避免破坏脚本 */
#sb_form {
  position: absolute !important;
  left: -9999px !important;
  top: auto !important;
  width: 1px !important;
  height: 1px !important;
  overflow: hidden !important;
  opacity: 0 !important;
  pointer-events: none !important;
}

/* 底部隐藏 */
#b_footer, .b_footnote, #b_pageFeedback, #b_feedback,
[role='contentinfo'], footer {
  display: none !important;
}

/* 分页保留 */
.b_pag, nav[aria-label*='Pagination'], nav[role='navigation'][aria-label*='页'] {
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

            // 🚀 启动时异步预加载每日一句（带本地兜底）
            _ = PreloadDailySentence();

            MainWindow.Activate();
            MainWindow.Closed += (s, e) => { isClosing = true; };
        }

        public DailySentenceData? CachedDailySentence { get; set; }
        public event Action? DailySentenceUpdated;

        public async Task PreloadDailySentence()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                string json = await _http.GetStringAsync("https://open.iciba.com/dsapi/", cts.Token);

                using var doc = JsonDocument.Parse(json);
                string val(string prop) =>
                    doc.RootElement.TryGetProperty(prop, out var el) ? (el.GetString() ?? "") : "";

                var data = new DailySentenceData
                {
                    Caption = val("caption"),
                    Date = val("dateline"),
                    En = val("content"),
                    Zh = val("note"),
                    TtsUrl = val("tts"),
                    PicUrl = val("picture2").Length > 0 ? val("picture2") :
                             val("picture").Length > 0 ? val("picture") : ""
                };

                // 如果有图片 URL，先下载并转成 Base64
                if (!string.IsNullOrWhiteSpace(data.PicUrl) &&
                    Uri.IsWellFormedUriString(data.PicUrl, UriKind.Absolute))
                {
                    try
                    {
                        using var ctsImg = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        byte[] bytes = await _http.GetByteArrayAsync(data.PicUrl, ctsImg.Token);

                        if (bytes.Length > 0)
                        {
                            var ext = data.PicUrl.ToLowerInvariant().EndsWith(".png") ? "image/png" :
                                      data.PicUrl.ToLowerInvariant().EndsWith(".webp") ? "image/webp" :
                                      "image/jpeg";

                            data.PicUrl = $"data:{ext};base64,{Convert.ToBase64String(bytes)}";
                        }
                    }
                    catch { /* 图片下载失败就保留原 URL 或置空 */ }
                }

                // 一次性更新，避免闪烁
                CachedDailySentence = data;
                DailySentenceUpdated?.Invoke();
            }
            catch
            {
                CachedDailySentence = new DailySentenceData
                {
                    Caption = "",
                    Date = "",
                    En = "每日一句暂不可用",
                    Zh = "",
                    PicUrl = "",
                    TtsUrl = ""
                };
                DailySentenceUpdated?.Invoke();
            }
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

            // 有道 HTML 注入
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
                            string extraDarkCss = isDark
                                ? @":root{color-scheme:dark;}
                           html,body{background:#0f0f0f!important;color:#ddd!important;}
                           a{color:#6fb1ff!important;}
                           .trans-container,.dict-module{background-color:#1e1e1e!important;color:#ddd!important;}"
                                : "";

                            string styleTag = $"<style>{HideCssRules}{extraDarkCss}[id*='feedback'],[class*='feedback']{{display:none!important;pointer-events:none!important;}}</style>";
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

            // 有道 CSS 注入
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

                    cssText += "\n" + HideCssRules +
                               "\n.trans-container,.dict-module{background-color:#1e1e1e!important;color:#ddd!important;}" +
                               "\n[id*='feedback'],[class*='feedback']{display:none!important;pointer-events:none!important;}";

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
                            string safeBingCss = BingHideCss.Replace(
                                "#sb_form,",
                                @"#sb_form { position:absolute !important;left:-9999px!important;top:auto!important;width:1px!important;height:1px!important;overflow:hidden!important;opacity:0!important;pointer-events:none!important; }"
                            );

                            string styleTag = $"<style>{safeBingCss}</style>";

                            bool isDark = MainWindow?.Content is FrameworkElement fe && fe.ActualTheme == ElementTheme.Dark;
                            if (isDark)
                            {
                                styleTag += "<meta name=\"color-scheme\" content=\"dark\">";
                            }
                            else
                            {
                                styleTag += "<meta name=\"color-scheme\" content=\"light\">";
                            }

                            html = html.Insert(closeHeadTag + 1, styleTag);
                        }
                    }

                    var ras = new InMemoryRandomAccessStream();
                    var writer = new DataWriter(ras)
                    {
                        UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8,
                        ByteOrder = Windows.Storage.Streams.ByteOrder.LittleEndian
                    };
                    writer.WriteBytes(Encoding.UTF8.GetBytes(html));
                    await writer.StoreAsync();
                    ras.Seek(0);
                    e.Response = core.Environment.CreateWebResourceResponse(
                        ras, 200, "OK", "Content-Type: text/html; charset=utf-8");
                }
                finally { deferral.Complete(); }
            };

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

            core.NavigationStarting += (s, e) =>
            {
                try
                {
                    var defaultUA = core.Settings.UserAgent;
                    if (!defaultUA.Contains("MyApp"))
                        core.Settings.UserAgent = defaultUA + " MyApp/1.0";

                    bool isDarkTheme = MainWindow?.Content is FrameworkElement fe2 &&
                                       fe2.ActualTheme == ElementTheme.Dark;
                    core.Profile.PreferredColorScheme = isDarkTheme
                        ? CoreWebView2PreferredColorScheme.Dark
                        : CoreWebView2PreferredColorScheme.Light;
                }
                catch { }
            };

            if (MainWindow?.Content is FrameworkElement feTheme)
            {
                feTheme.ActualThemeChanged += (_, __) =>
                {
                    try
                    {
                        bool isDarkTheme = feTheme.ActualTheme == ElementTheme.Dark;
                        core.Profile.PreferredColorScheme = isDarkTheme
                            ? CoreWebView2PreferredColorScheme.Dark
                            : CoreWebView2PreferredColorScheme.Light;

                        if (!string.IsNullOrEmpty(core.Source) &&
                            core.Source.Contains("bing.com", StringComparison.OrdinalIgnoreCase))
                        {
                            core.Reload();
                        }
                    }
                    catch { }
                };
            }
            await Task.CompletedTask;
        }
    }
}