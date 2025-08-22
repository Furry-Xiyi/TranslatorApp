using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using TranslatorApp.Services;

namespace TranslatorApp.Pages
{
    public sealed partial class WordLookupPage : Page
    {
        private string _currentQuery = string.Empty;
        private ComboBox _cbSite;
        private AutoSuggestBox _searchBox;

        public WordLookupPage()
        {
            InitializeComponent();
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

            sender.ItemsSource = new List<string>
            {
                text,
                $"{text} meaning",
                $"{text} 翻译",
                $"{text} 用法"
            };
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var q = args.QueryText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(q)) return;

            _currentQuery = q;
            NavigateToSite(q);
        }

        private void NavigateToSite(string query)
        {
            var site = (_cbSite.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Youdao";
            SettingsService.LastLookupSite = site;

            string url = site switch
            {
                "Google" => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
                "Baidu" => $"https://www.baidu.com/s?wd={Uri.EscapeDataString(query)}",
                _ => $"https://dict.youdao.com/result?word={Uri.EscapeDataString(query)}&lang=en"
            };

            Web.Source = new Uri(url);
        }

        private void FabFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_currentQuery))
            {
                FavoritesService.Add(_currentQuery);
            }
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (App.MainWindow != null)
            {
                // 居中容器（加到中列）
                var centerPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // 下拉框
                _cbSite = new ComboBox
                {
                    Width = 140
                };
                _cbSite.Items.Add(new ComboBoxItem { Content = "Google", Tag = "Google" });
                _cbSite.Items.Add(new ComboBoxItem { Content = "百度", Tag = "Baidu" });
                _cbSite.Items.Add(new ComboBoxItem { Content = "有道", Tag = "Youdao" });

                // 恢复上次选择
                foreach (ComboBoxItem item in _cbSite.Items)
                {
                    if ((item.Tag?.ToString() ?? "") == SettingsService.LastLookupSite)
                    {
                        _cbSite.SelectedItem = item;
                        break;
                    }
                }
                if (_cbSite.SelectedItem == null)
                    _cbSite.SelectedIndex = 2; // 默认有道

                _cbSite.SelectionChanged += (s, ev) =>
                {
                    if (!string.IsNullOrWhiteSpace(_searchBox.Text))
                        NavigateToSite(_searchBox.Text);
                };

                // 搜索框
                _searchBox = new AutoSuggestBox
                {
                    Width = 400,
                    PlaceholderText = "输入要查的词汇...",
                    QueryIcon = new SymbolIcon(Symbol.Find)
                };
                _searchBox.TextChanged += SearchBox_TextChanged;
                _searchBox.QuerySubmitted += SearchBox_QuerySubmitted;

                // 加入居中容器
                centerPanel.Children.Add(_cbSite);
                centerPanel.Children.Add(_searchBox);

                // 把中列的容器清空并添加“居中容器”
                App.MainWindow.TitleBarCenterPanel.Children.Clear();
                App.MainWindow.TitleBarCenterPanel.Children.Add(centerPanel);
            }

            if (e.Parameter is string term && !string.IsNullOrWhiteSpace(term))
            {
                _searchBox.Text = term;
                _currentQuery = term;
                NavigateToSite(term);
            }
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            // 离开时只清空“中列”，左侧图标/标题依旧存在
            App.MainWindow?.TitleBarCenterPanel.Children.Clear();
        }
    }
}