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
                $"{text} ����",
                $"{text} �÷�"
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
                // �����������ӵ����У�
                var centerPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // ������
                _cbSite = new ComboBox
                {
                    Width = 140
                };
                _cbSite.Items.Add(new ComboBoxItem { Content = "Google", Tag = "Google" });
                _cbSite.Items.Add(new ComboBoxItem { Content = "�ٶ�", Tag = "Baidu" });
                _cbSite.Items.Add(new ComboBoxItem { Content = "�е�", Tag = "Youdao" });

                // �ָ��ϴ�ѡ��
                foreach (ComboBoxItem item in _cbSite.Items)
                {
                    if ((item.Tag?.ToString() ?? "") == SettingsService.LastLookupSite)
                    {
                        _cbSite.SelectedItem = item;
                        break;
                    }
                }
                if (_cbSite.SelectedItem == null)
                    _cbSite.SelectedIndex = 2; // Ĭ���е�

                _cbSite.SelectionChanged += (s, ev) =>
                {
                    if (!string.IsNullOrWhiteSpace(_searchBox.Text))
                        NavigateToSite(_searchBox.Text);
                };

                // ������
                _searchBox = new AutoSuggestBox
                {
                    Width = 400,
                    PlaceholderText = "����Ҫ��Ĵʻ�...",
                    QueryIcon = new SymbolIcon(Symbol.Find)
                };
                _searchBox.TextChanged += SearchBox_TextChanged;
                _searchBox.QuerySubmitted += SearchBox_QuerySubmitted;

                // �����������
                centerPanel.Children.Add(_cbSite);
                centerPanel.Children.Add(_searchBox);

                // �����е�������ղ���ӡ�����������
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

            // �뿪ʱֻ��ա����С������ͼ��/�������ɴ���
            App.MainWindow?.TitleBarCenterPanel.Children.Clear();
        }
    }
}