using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
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

        // 原默认地址已弃用，这里备用但不会直接用
        private const string DefaultArticleUrl = "https://www.ef.com.cn/english-resources/english-vocabulary/";

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

            if (site == "Bing")
            {
                Web.Source = new Uri($"https://cn.bing.com/dict/search?q={Uri.EscapeDataString(query)}");
                return;
            }

            string url = site switch
            {
                "Google" => $"https://www.google.com/search?q=define%3A{Uri.EscapeDataString(query)}",
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
                NavigateToSite(term);
                await WaitForNextNavigationAsync();
            }
            else
            {
                await ShowDailySentenceFromIciba(); // 启动时直接加载金山每日一句
                await WaitForNextNavigationAsync();
            }

            Web.Opacity = 1;
        }

        private async Task EnsureWebReadyAsync()
        {
            try
            {
                await Web.EnsureCoreWebView2Async();
                await App.InitWebView2Async(Web.CoreWebView2);
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

        // ===== 每日一句（金山词霸中英版） =====
        private async Task ShowDailySentenceFromIciba()
        {
            try
            {
                using var client = new HttpClient();
                var json = await client.GetStringAsync("http://open.iciba.com/dsapi/");
                using var doc = JsonDocument.Parse(json);

                var en = doc.RootElement.GetProperty("content").GetString();
                var zh = doc.RootElement.GetProperty("note").GetString();
                var pic = doc.RootElement.TryGetProperty("picture2", out var p) ? p.GetString() : "";

                bool isDark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
                var bgColor = isDark ? "#1e1e1e" : "#f9f9f9";
                var textColor = isDark ? "#f0f0f0" : "#333";
                var subTextColor = isDark ? "#ccc" : "#666";

                var html = $@"
<html>
<head>
<meta charset='utf-8'/>
<style>
body {{ font-family: 'Segoe UI', sans-serif; padding:20px; background-color:{bgColor}; }}
.en {{ font-size:1.3em; color:{textColor}; margin-top:20px; }}
.zh {{ font-size:1em; color:{subTextColor}; margin-top:10px; }}
img {{ max-width:100%; margin-top:20px; border-radius:8px; }}
</style>
</head>
<body>
<div class='en'>{en}</div>
<div class='zh'>{zh}</div>
{(string.IsNullOrEmpty(pic) ? "" : $"<img src='{pic}' />")}
</body>
</html>";
                Web.NavigateToString(html);
            }
            catch
            {
                Web.NavigateToString("<html><body><h2>Keep learning!</h2></body></html>");
            }
        }
    }
}