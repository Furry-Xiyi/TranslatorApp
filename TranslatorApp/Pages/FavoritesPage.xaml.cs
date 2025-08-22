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
            // 跳转到查词翻译页，使用“上次所选网站”
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