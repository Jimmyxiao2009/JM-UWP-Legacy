using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Jm.Uwp
{
    public sealed partial class BookmarksPage : Page
    {
        public ObservableCollection<Bookmark> Items { get; } = new ObservableCollection<Bookmark>();

        public BookmarksPage()
        {
            InitializeComponent();
            List.ItemsSource = Items;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ReloadAsync();
        }

        private async System.Threading.Tasks.Task ReloadAsync()
        {
            Items.Clear();
            foreach (var b in await BookmarkStore.LoadAsync())
                Items.Add(b);
            EmptyText.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Bookmark b)
                Frame.Navigate(typeof(ReaderPage), new ReaderArgs
                {
                    AlbumId = b.AlbumId,
                    AlbumName = b.AlbumName,
                    ChapterId = b.ChapterId,
                    ChapterName = b.ChapterName,
                    JumpToPage = b.Page,
                });
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Bookmark b)
            {
                await BookmarkStore.RemoveAsync(b);
                await ReloadAsync();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
