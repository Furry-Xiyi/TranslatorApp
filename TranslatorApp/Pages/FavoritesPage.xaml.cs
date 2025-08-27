using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Animation;
using TranslatorApp.Models;
using TranslatorApp.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System;

namespace TranslatorApp.Pages
{
    public sealed partial class FavoritesPage : Page
    {
        public ObservableCollection<FavoriteItem> Favorites { get; }

        private enum ViewMode { List, Tile }
        private static ViewMode _currentViewMode = ViewMode.List;

        public FavoritesPage()
        {
            FavoritesService.Load();
            Favorites = FavoritesService.Items ?? new ObservableCollection<FavoriteItem>();

            InitializeComponent();

            List.ItemsSource = Favorites;

            ApplySorting();

            if (_currentViewMode == ViewMode.Tile)
            {
                TileViewToggle.IsChecked = true;
                ListViewToggle.IsChecked = false;
                ApplyTileView();
            }
            else
            {
                ListViewToggle.IsChecked = true;
                TileViewToggle.IsChecked = false;
                ApplyListView();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // 标题栏完全由 MainWindow 托管，这里不再操作标题栏
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // 不再操作标题栏，也无需解绑标题栏事件（事件绑定在 MainWindow 上）
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FavoriteItem item)
            {
                Frame.Navigate(typeof(WordLookupPage), item.Term);
            }
        }

        private void SwipeDelete_Invoked(SwipeItem sender, SwipeItemInvokedEventArgs args)
        {
            if (args.SwipeControl.DataContext is FavoriteItem item)
            {
                FavoritesService.Remove(item);
                ShowDeleteInfoBar("已删除收藏");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is FavoriteItem item)
            {
                FavoritesService.Remove(item);
                ShowDeleteInfoBar("已删除收藏");
            }
        }

        private void SortSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySorting();
        }

        private void ApplySorting()
        {
            if (SortSelector?.SelectedItem is ComboBoxItem selected && selected.Tag is string tag)
            {
                var sorted = tag switch
                {
                    "NameAsc" => Favorites.OrderBy(f => f.Term).ToList(),
                    "NameDesc" => Favorites.OrderByDescending(f => f.Term).ToList(),
                    "DateNewest" => Favorites.OrderByDescending(f => f.AddedOn).ToList(),
                    "DateOldest" => Favorites.OrderBy(f => f.AddedOn).ToList(),
                    _ => Favorites.ToList()
                };

                Favorites.Clear();
                foreach (var item in sorted)
                    Favorites.Add(item);
            }
        }

        private void ListViewToggle_Click(object sender, RoutedEventArgs e)
        {
            ListViewToggle.IsChecked = true;
            TileViewToggle.IsChecked = false;
            ApplyListView();
            _currentViewMode = ViewMode.List;
        }

        private void TileViewToggle_Click(object sender, RoutedEventArgs e)
        {
            ListViewToggle.IsChecked = false;
            TileViewToggle.IsChecked = true;
            ApplyTileView();
            _currentViewMode = ViewMode.Tile;
        }

        private void ApplyListView()
        {
            var listPanelXaml =
                "<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
                "  <ItemsStackPanel Orientation='Vertical'/>" +
                "</ItemsPanelTemplate>";
            var listItemsPanel = (ItemsPanelTemplate)XamlReader.Load(listPanelXaml);
            List.ItemsPanel = listItemsPanel;

            List.ItemTemplate = (DataTemplate)Resources["ListItemTemplate"];
            List.ItemContainerStyle = null;
            List.HorizontalContentAlignment = HorizontalAlignment.Stretch;

            if (List.ItemsSource == null)
                List.ItemsSource = Favorites;
        }

        private void ApplyTileView()
        {
            List.ItemsPanel = (ItemsPanelTemplate)Resources["TileItemsPanel"];
            List.ItemTemplate = (DataTemplate)Resources["TileItemTemplate"];
            List.ItemContainerStyle = (Style)Resources["TileListViewItemStyle"];

            if (List.ItemsSource == null)
                List.ItemsSource = Favorites;
        }


        public void ApplySearchFilter(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                List.ItemsSource = Favorites;
            }
            else
            {
                var filtered = Favorites
                    .Where(f => !string.IsNullOrEmpty(f.Term) &&
                                f.Term.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                List.ItemsSource = new ObservableCollection<FavoriteItem>(filtered);
            }
        }
        public void OnFavoritesSearchTextChanged(string? text)
        {
            ApplySearchFilter(text);
        }

        public void OnFavoritesSearchQuerySubmitted(string? text)
        {
            ApplySearchFilter(text);
        }

        private void ShowDeleteInfoBar(string message)
        {
            DeleteInfoBar.Message = message;
            DeleteInfoBar.IsOpen = true;

            var slideIn = new DoubleAnimation
            {
                From = -40,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            var sbIn = new Storyboard();
            Storyboard.SetTarget(slideIn, DeleteInfoBar);
            Storyboard.SetTargetProperty(slideIn, "(UIElement.RenderTransform).(TranslateTransform.Y)");
            sbIn.Children.Add(slideIn);

            sbIn.Completed += (s, e) =>
            {
                var slideOut = new DoubleAnimation
                {
                    From = 0,
                    To = -40,
                    Duration = TimeSpan.FromMilliseconds(200),
                    BeginTime = TimeSpan.FromSeconds(2)
                };
                var sbOut = new Storyboard();
                Storyboard.SetTarget(slideOut, DeleteInfoBar);
                Storyboard.SetTargetProperty(slideOut, "(UIElement.RenderTransform).(TranslateTransform.Y)");
                sbOut.Children.Add(slideOut);
                sbOut.Completed += (s2, e2) => DeleteInfoBar.IsOpen = false;
                sbOut.Begin();
            };

            sbIn.Begin();
        }
    }
}