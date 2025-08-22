using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TranslatorApp.Models;
using TranslatorApp.Services;

namespace TranslatorApp.Pages;

public sealed partial class FavoritesPage : Page
{
    public FavoritesPage()
    {
        InitializeComponent();
        List.ItemsSource = FavoritesService.Items;
    }

    private void List_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FavoriteItem item)
        {
            // ��ת����ʷ���ҳ��ʹ�á��ϴ���ѡ��վ��
            Frame.Navigate(typeof(WordLookupPage), item.Term);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is FavoriteItem item)
        {
            FavoritesService.Remove(item);
        }
    }
}