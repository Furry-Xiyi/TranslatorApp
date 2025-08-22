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
        // �ָ��ϴ�ѡ�����վ
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
        // �޸���WinUI 3 ��û�� CheckCurrent ���ԣ����� Reason �ж�
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        // TODO: �ɽ����Ӧվ������� API������ʾ�����ؾ�̬����
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