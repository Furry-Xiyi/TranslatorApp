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

            // 订阅每日一句更新事件
            var app = (App)Application.Current;
            app.DailySentenceUpdated += OnDailySentenceUpdated;

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

            // 取消订阅每日一句更新事件
            var app = (App)Application.Current;
            app.DailySentenceUpdated -= OnDailySentenceUpdated;
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
                if (!string.IsNullOrEmpty(App.DailySentenceHtml))
                {
                    Web.NavigateToString(App.DailySentenceHtml);
                }
                else
                {
                    Web.NavigateToString("<html><body><h2>Loading...</h2></body></html>");
                }
                await WaitForNextNavigationAsync();
            }

            Web.Opacity = 1;
        }

        // 每日一句更新事件回调
        private async void OnDailySentenceUpdated()
        {
            if (Web.CoreWebView2 == null)
            {
                await Web.EnsureCoreWebView2Async();
            }
            Web.NavigateToString(App.DailySentenceHtml);
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
    }
}