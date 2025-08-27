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

        public event Action? DailySentenceUpdated;

        public async Task PreloadDailySentence()
        {
            try
            {
                // 显示加载动画
                DailySentenceHtml = @"
<html>
<head>
<meta charset='utf-8'/>
<meta name='color-scheme' content='light dark'/>
<style>
body { font-family: 'Segoe UI', sans-serif; display:flex;align-items:center;justify-content:center;
       height:100vh; background-color:transparent; color:gray; margin:0; }
.spinner {
  border: 4px solid rgba(0,0,0,0.1);
  width: 36px; height: 36px;
  border-radius: 50%;
  border-left-color: #09f;
  animation: spin 1s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }
</style>
</head>
<body>
<div class='spinner'></div>
</body>
</html>";
                DailySentenceUpdated?.Invoke();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var json = await _http.GetStringAsync("https://open.iciba.com/dsapi/", cts.Token);
                using var doc = JsonDocument.Parse(json);

                // 编码工具
                string enc(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");
                string encAttr(string s) => Uri.EscapeUriString(s ?? "");

                var en = enc(doc.RootElement.GetProperty("content").GetString());
                var zh = enc(doc.RootElement.GetProperty("note").GetString());
                var caption = enc(doc.RootElement.TryGetProperty("caption", out var cap) ? cap.GetString() : "");
                var date = enc(doc.RootElement.TryGetProperty("dateline", out var dl) ? dl.GetString() : "");
                var tts = encAttr(doc.RootElement.TryGetProperty("tts", out var t) ? t.GetString() ?? "" : "");

                var picUrl = encAttr(
                    (doc.RootElement.TryGetProperty("picture2", out var p2) ? p2.GetString() : null)
                    ?? (doc.RootElement.TryGetProperty("picture", out var p1) ? p1.GetString() : null)
                    ?? ""
                );

                bool isDark = Current.RequestedTheme == ApplicationTheme.Dark;
                var bgColor = isDark ? "#1e1e1e" : "#f9f9f9";
                var textColor = isDark ? "#f0f0f0" : "#333";
                var subTextColor = isDark ? "#ccc" : "#666";

                var ttsHtml = string.IsNullOrWhiteSpace(tts) ? "" :
                    $"<audio controls style='margin-top:12px; width:100%'><source src='{tts}' type='audio/mpeg'></audio>";

                var imgHtml = string.IsNullOrWhiteSpace(picUrl) ? "" :
                    $"<img src='{picUrl}' />";

                DailySentenceHtml = $@"
<html>
<head>
<meta charset='utf-8'/>
<meta name='color-scheme' content='{(isDark ? "dark" : "light")}'/>
<style>
body {{ font-family: 'Segoe UI', sans-serif; padding:20px; background-color:{bgColor}; margin:0; }}
.header {{ color:{subTextColor}; font-size:0.9em; }}
.en {{ font-size:1.3em; color:{textColor}; margin-top:14px; line-height:1.6; }}
.zh {{ font-size:1em; color:{subTextColor}; margin-top:8px; }}
img {{ max-width:100%; margin-top:16px; border-radius:8px; }}
audio {{ outline:none; }}
</style>
</head>
<body>
<div class='header'>{caption} {date}</div>
<div class='en'>{en}</div>
<div class='zh'>{zh}</div>
{imgHtml}
{ttsHtml}
</body>
</html>";

                DailySentenceUpdated?.Invoke();

                // 内嵌 Base64 图片
                if (!string.IsNullOrWhiteSpace(picUrl) && Uri.IsWellFormedUriString(picUrl, UriKind.Absolute))
                {
                    try
                    {
                        using var ctsImg = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        var bytes = await _http.GetByteArrayAsync(picUrl, ctsImg.Token);
                        if (bytes?.Length > 0)
                        {
                            var ext = picUrl.ToLowerInvariant().EndsWith(".png") ? "image/png" :
                                      picUrl.ToLowerInvariant().EndsWith(".webp") ? "image/webp" :
                                      "image/jpeg";
                            var b64 = Convert.ToBase64String(bytes);
                            var base64ImgHtml = $"<img src='data:{ext};base64,{b64}' />";
                            DailySentenceHtml = DailySentenceHtml.Replace(imgHtml, base64ImgHtml);
                            DailySentenceUpdated?.Invoke();
                        }
                    }
                    catch (TaskCanceledException) { }
                }
            }
            catch
            {
                DailySentenceHtml = "<html><body><h2>每日一句暂不可用</h2></body></html>";
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
        }
    }
}