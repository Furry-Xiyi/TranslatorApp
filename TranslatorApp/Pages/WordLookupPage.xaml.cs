using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using TranslatorApp.Services;

namespace TranslatorApp.Pages
{
    public sealed partial class WordLookupPage : Page
    {
        private string _currentQuery = string.Empty;
        private ComboBox _cbSite;
        private AutoSuggestBox _searchBox;
        private List<string> _history = new();

        private const string DefaultArticleUrl = "https://www.ef.com.cn/english-resources/english-vocabulary/";
        private const string ForcedStyleId = "app-forced-dark-style";

        private string _docStartDarkCssToken;
        private string _docStartBingFixToken;
        private bool _youdaoWarmed;

        public WordLookupPage()
        {
            InitializeComponent();
        }

        private void LoadHistory() => _history = SettingsService.LookupHistory ?? new List<string>();
        private void SaveHistory() => SettingsService.LookupHistory = _history;

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
            var site = (_cbSite.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Youdao";
            SettingsService.LastLookupSite = site;

            string url = site switch
            {
                "Google" => $"https://www.google.com/search?q=define%3A{Uri.EscapeDataString(query)}",
                "Bing" => $"https://cn.bing.com/search?q=define+{Uri.EscapeDataString(query)}",
                "Youdao" => $"https://dict.youdao.com/result?word={Uri.EscapeDataString(query)}&lang=en",
                _ => $"https://dict.youdao.com/result?word={Uri.EscapeDataString(query)}&lang=en"
            };

            Web.Source = new Uri(url);
        }

        private void FabFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_currentQuery))
                FavoritesService.Add(_currentQuery);
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadHistory();

            if (App.MainWindow != null)
            {
                var centerPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                _cbSite = new ComboBox { Width = 140 };
                _cbSite.Items.Add(new ComboBoxItem { Content = "Google", Tag = "Google" });
                _cbSite.Items.Add(new ComboBoxItem { Content = "Bing", Tag = "Bing" });
                _cbSite.Items.Add(new ComboBoxItem { Content = "Youdao", Tag = "Youdao" });

                foreach (ComboBoxItem item in _cbSite.Items)
                {
                    if ((item.Tag?.ToString() ?? "") == SettingsService.LastLookupSite)
                    {
                        _cbSite.SelectedItem = item;
                        break;
                    }
                }
                if (_cbSite.SelectedItem == null) _cbSite.SelectedIndex = 2;

                _cbSite.SelectionChanged += (s, ev) =>
                {
                    var site = (_cbSite.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                    if (!string.IsNullOrEmpty(site))
                        SettingsService.LastLookupSite = site;
                    if (!string.IsNullOrWhiteSpace(_searchBox.Text))
                        NavigateToSite(_searchBox.Text);
                };

                _searchBox = new AutoSuggestBox
                {
                    Width = 400,
                    PlaceholderText = "输入要查的词汇...",
                    QueryIcon = new SymbolIcon(Symbol.Find)
                };
                _searchBox.TextChanged += SearchBox_TextChanged;
                _searchBox.QuerySubmitted += SearchBox_QuerySubmitted;

                centerPanel.Children.Add(_cbSite);
                centerPanel.Children.Add(_searchBox);
                App.MainWindow.TitleBarCenterPanel.Children.Clear();
                App.MainWindow.TitleBarCenterPanel.Children.Add(centerPanel);
            }

            Web.Opacity = 0;
            InitializeStartupNavigation(e.Parameter);
        }
        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            App.MainWindow?.TitleBarCenterPanel.Children.Clear();
        }

        private async void InitializeStartupNavigation(object navParam)
        {
            await EnsureWebReadyAsync();

            if (navParam is string term && !string.IsNullOrWhiteSpace(term))
            {
                _searchBox.Text = term;
                _currentQuery = term;
                AddToHistory(term);
                // 已移除 WarmUpYoudaoAsync 调用，避免后台预加载导致 404 弹出
                NavigateToSite(term);
                await WaitForNextNavigationAsync();
            }
            else
            {
                // 同样移除 WarmUpYoudaoAsync 调用
                Web.Source = new Uri(DefaultArticleUrl);
                await WaitForNextNavigationAsync();
            }

            Web.Opacity = 1;
        }

        private async Task EnsureWebReadyAsync()
        {
            try
            {
                await Web.EnsureCoreWebView2Async();
                ApplyPreferredColorScheme();
                await EnsureDocumentStartScripts();
                Web.NavigationCompleted += (s, e) => ApplyPageTheme();
            }
            catch { }

            this.ActualThemeChanged += (s, e3) =>
            {
                ApplyPreferredColorScheme();
                ApplyPageTheme();
            };
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

        private void ApplyPreferredColorScheme()
        {
            if (Web?.CoreWebView2 == null) return;
            var preferred = CoreWebView2PreferredColorScheme.Auto;
            if (this.ActualTheme == ElementTheme.Dark)
                preferred = CoreWebView2PreferredColorScheme.Dark;
            else if (this.ActualTheme == ElementTheme.Light)
                preferred = CoreWebView2PreferredColorScheme.Light;
            Web.CoreWebView2.Profile.PreferredColorScheme = preferred;
        }

        private void ApplyPageTheme()
        {
            var host = Web.Source?.Host?.ToLowerInvariant() ?? string.Empty;
            bool supportsNative = SupportsNativeDark(host);

            if (this.ActualTheme == ElementTheme.Dark)
            {
                if (supportsNative)
                    RemoveForcedDarkStyle();
                else
                    InjectForcedDarkStyle(host);
            }
            else
            {
                RemoveForcedDarkStyle();
            }
        }

        private static bool SupportsNativeDark(string host)
        {
            return host.Contains("google.") || host.Contains("bing.com");
        }

        private async void RemoveForcedDarkStyle()
        {
            if (Web?.CoreWebView2 == null) return;
            await Web.CoreWebView2.ExecuteScriptAsync($@"
                (function(){{
                    var s = document.getElementById('{ForcedStyleId}');
                    if (s) s.remove();
                }})();
            ");
        }

        private async void InjectForcedDarkStyle(string host)
        {
            if (Web?.CoreWebView2 == null) return;
            string css = BuildDarkCssForHost(host);
            await Web.CoreWebView2.ExecuteScriptAsync($@"
                (function(){{
                    var old = document.getElementById('{ForcedStyleId}');
                    if (old) old.remove();
                    var style = document.createElement('style');
                    style.id = '{ForcedStyleId}';
                    style.innerHTML = `{css}`;
                    (document.head || document.documentElement).appendChild(style);
                }})();
            ");
        }

        private static string BuildDarkCssForHost(string host)
        {
            if (host.Contains("youdao.com"))
            {
                return @"
/* 顶部整块背景强制深色兜底 */
html, body,
body > div,
body > div:first-child,
header, header *:not(img):not(svg) {
  background: #0f0f0f !important;
  color: #ddd !important;
  background-image: none !important;
}

/* 隐藏顶部标题栏和搜索框 */
header,
.top-nav, .nav, .header, .nav-bar, .nav-wrap,
.search-wrapper, .search-area, .search-bar-container, .search-bar-bg,
header .search, header .search-wrapper, header .search-box,
.search-box, .search-container, .search-bar-wrap {
  display: none !important;
}

/* 隐藏底部版权/页脚 */
footer, .footer, .yd-footer, .global-footer, .ft, .ft-wrap, .ft-container,
[class*='footer'], [id*='footer'], [class*='copyright'], [id*='copyright'] {
  display: none !important;
}

/* 基础与色系 */
:root{ color-scheme: dark; }
html, body{
  background:#0f0f0f !important;
  color:#ddd !important;
}

/* 主体容器 */
#container, .container, .content, .result, .dict-root, .yd-dict,
.word-exp, .trans-container, .basic, .collins, .oxford, .exam_type,
.examples, .webPhrase, .wordGroup, .pr-container, .cigen, .section,
.card, .panel, .module, .wrap, .main, .aside, .layout, .page, .list,
.item, .detail, .means, .entry, .summary, .definitions, .examps{
  background:#141414 !important;
  color:#ddd !important;
  border-color:#242424 !important;
}

/* 文字统一浅色 */
body :not(svg):not(path):not(img):not(video){
  color: #ddd !important;
}

/* 标题/关键词 */
h1,h2,h3,h4,h5,h6,.title,.keyword,.pos,.phonetic,.exam_type .title,
.section .title,.module .title,.card .title{
  color:#8ec7ff !important;
}

/* 次级块 */
.block,.sub-block,.subsection,.note,.tip,.tag,.badge,
.trans-container .word,.collins .entry,.oxford .entry,
.examples .example,.webPhrase .wordGroup,.list .item,
.entry .sense,.entry .collins-section{
  background:#1a1a1a !important;
  color:#ddd !important;
  border-color:#2a2a2a !important;
}

/* 链接 */
a{ color:#6fb1ff !important; }
a:hover{ color:#9ad0ff !important; }

/* 输入与占位符 */
input, textarea, select, button, .search, .search-input, .search-bar, .search-box{
  background:#1c1c1c !important;
  color:#e6e6e6 !important;
  border:1px solid #2f2f2f !important;
}
input::placeholder, textarea::placeholder{
  color:#aaaaaa !important;
}
button, .btn, .action{
  background:#232323 !important;
  color:#e6e6e6 !important;
  border-color:#2f2f2f !important;
}
button:hover, .btn:hover{
  background:#2b2b2b !important;
}

/* 表格/分隔 */
table{ background:#141414 !important; color:#ddd !important; }
th, td{ border-color:#2a2a2a !important; }
hr, .divider{ border-color:#2a2a2a !important; }

/* 高亮/代码 */
mark, .highlight{
  background:#3a3a00 !important;
  color:#fff !important;
}
code, pre{
  background:#171717 !important;
  color:#d6d6d6 !important;
  border:1px solid #2a2a2a !important;
}

/* 卡片弱化 */
.card, .panel, .module, .entry, .example{
  box-shadow:none !important;
  border-color:#2a2a2a !important;
}

/* 图片/图标反色（黑白反转） */
img, svg, video{
  filter: invert(1) hue-rotate(180deg) !important;
  mix-blend-mode:normal !important;
}

/* 白底卡片 */
[class*='card'], [class*='panel'], [class*='box'], [class*='wrap']{
  background:#181818 !important;
  color:#ddd !important;
  border-color:#2f2f2f !important;
}

/* 广告隐藏 */
iframe[src*='ad'], iframe[src*='ads'], .ad, .ads, .advert, .advertisement{
  display:none !important;
}
";
            }

            return @"
:root{ color-scheme: dark; }
html,body{ background:#0f0f0f !important; color:#ddd !important; }
a{ color:#6fb1ff !important; }
table,th,td{ border-color:#2a2a2a !important; }
input,textarea,select,button{
  background:#1c1c1c !important; 
  color:#e6e6e6 !important; 
  border:1px solid #2f2f2f !important;
}
";
        }

        private async Task EnsureDocumentStartScripts()
        {
            if (Web?.CoreWebView2 == null) return;

            // 无条件隐藏顶部和底部（浅色/深色都生效）+ DOM 删除双保险（增加浅色模式类名）
            var hideHeaderFooterScript = @"
(function(){
  try{
    const HIDE_ID = 'always-hide-header-footer';
    const css = '\u0060
      header,
      .top-nav, .nav, .header, .nav-bar, .nav-wrap, .header-light,
      .search-wrapper, .search-area, .search-bar-container, .search-bar-bg,
      header .search, header .search-wrapper, header .search-box,
      .search-box, .search-container, .search-bar-wrap,
      footer, .footer, .yd-footer, .global-footer, .ft, .ft-wrap, .ft-container,
      .footer-light, .light-footer,
      [class*=""footer""], [id*=""footer""], [class*=""copyright""], [id*=""copyright""] {
        display: none !important;
      }
    \u0060';
    // 注入CSS
    const style = document.createElement('style');
    style.id = HIDE_ID;
    style.innerHTML = css;
    (document.head || document.documentElement).appendChild(style);

    // DOM 删除双保险
    const hideNodes = () => {
      document.querySelectorAll(
        'header, .top-nav, .nav, .header, .nav-bar, .nav-wrap, .header-light,' +
        '.search-wrapper, .search-area, .search-bar-container, .search-bar-bg,' +
        'header .search, header .search-wrapper, header .search-box,' +
        '.search-box, .search-container, .search-bar-wrap,' +
        'footer, .footer, .yd-footer, .global-footer, .ft, .ft-wrap, .ft-container,' +
        '.footer-light, .light-footer,' +
        '[class*=""footer""], [id*=""footer""], [class*=""copyright""], [id*=""copyright""]'
      ).forEach(el => el.remove());
    };
    hideNodes();
    new MutationObserver(hideNodes).observe(document.documentElement, { childList: true, subtree: true });
  }catch(e){}
})();
";
            await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(hideHeaderFooterScript);

            if (string.IsNullOrEmpty(_docStartDarkCssToken))
            {
                var docStartDarkCss = @"
(function(){
  try{
    const host = location.host.toLowerCase();
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const supportsNative = host.includes('google.') || host.includes('bing.com');
    if (!(prefersDark && !supportsNative)) return;

    const ID = '" + ForcedStyleId + @"';
    const css = `" + BuildDarkCssForHost("youdao.com").Replace("`", "\\`") + @"`;

    const inject = () => {
      if (document.getElementById(ID)) return;
      const style = document.createElement('style');
      style.id = ID;
      style.innerHTML = css;
      (document.head || document.documentElement).appendChild(style);
    };

    inject();
    const mo = new MutationObserver(() => {
      if (!document.getElementById(ID)) inject();
    });
    mo.observe(document.documentElement, { childList:true, subtree:true });
  }catch(_){}
})();
";
                _docStartDarkCssToken = await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(docStartDarkCss);
            }

            if (string.IsNullOrEmpty(_docStartBingFixToken))
            {
                var docStartBingFix = @"
(function(){
  try{
    const host = location.host.toLowerCase();
    if (!host.endsWith('bing.com')) return;
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    try{ localStorage.setItem('bing.theme','system'); }catch(e){}
    try{
      const root = document.documentElement || document.body;
      if (root){
        root.removeAttribute('data-theme');
        root.setAttribute('data-theme', prefersDark ? 'dark' : 'light');
      }
    }catch(e){}
    try{
      document.cookie = 'DARK='+(prefersDark? '1':'0')+'; domain=.bing.com; path=/; max-age=31536000; SameSite=Lax';
    }catch(e){}
    setTimeout(function(){
      try{
        const prefersDark2 = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        const root2 = document.documentElement || document.body;
        if (root2){
          root2.setAttribute('data-theme', prefersDark2 ? 'dark' : 'light');
        }
      }catch(e){}
    }, 300);
  }catch(_){}
})();
";
                _docStartBingFixToken = await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(docStartBingFix);
            }
        }
    }
}