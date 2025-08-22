using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using TranslatorApp.Services;

namespace TranslatorApp.Pages;

public sealed partial class WordLookupPage : Page
{
    private string _currentQuery = string.Empty;

    public WordLookupPage()
    {
        InitializeComponent();
        // 恢复上次选择的网站
        SetSite(SettingsService.LastLookupSite);
    }

    private void SetSite(string site)
    {
        foreach (ComboBoxItem item in CbSite.Items)
        {
            if ((item.Tag?.ToString() ?? "") == site)
            {
                CbSite.SelectedItem = item;
                break;
            }
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // 修复：WinUI 3 中没有 CheckCurrent 属性，改用 Reason 判断
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        // TODO: 可接入对应站点的联想 API，这里示例返回静态建议
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
        var site = (CbSite.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Youdao";
        SettingsService.LastLookupSite = site;

        string url = site switch
        {
            "Google" => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
            "Baidu" => $"https://www.baidu.com/s?wd={Uri.EscapeDataString(query)}",
            _ => $"https://dict.youdao.com/result?word={Uri.EscapeDataString(query)}&lang=en" // Youdao
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
        if (e.Parameter is string term && !string.IsNullOrWhiteSpace(term))
        {
            SearchBox.Text = term;
            _currentQuery = term;
            NavigateToSite(term);
        }
    }
}