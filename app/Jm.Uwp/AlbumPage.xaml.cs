using System;
using System.Collections.ObjectModel;
using Jm.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Jm.Uwp
{
    public sealed partial class AlbumPage : Page
    {
        private readonly JmClient _client = App.Jm;
        private JmAlbumDetail _album;
        public ObservableCollection<JmSeries> Chapters { get; } = new ObservableCollection<JmSeries>();

        public AlbumPage()
        {
            InitializeComponent();
            ChapterList.ItemsSource = Chapters;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (!(e.Parameter is string id) || !int.TryParse(id, out var aid))
            {
                TitleText.Text = "无效的专辑 id";
                return;
            }

            Busy.IsActive = true;
            try
            {
                var result = await _client.GetAlbumAsync(aid);
                if (!result.IsOk)
                {
                    TitleText.Text = $"加载失败：code={result.Code} {result.ErrorMsg}";
                    return;
                }

                var album = result.AsAlbum();
                _album = album;
                TitleText.Text = album.Name;
                MetaText.Text = $"JM{album.Id}  作者:{string.Join("/", album.Author)}  ♥{album.Likes}  👁{album.TotalViews}";
                if (!string.IsNullOrEmpty(album.CoverUrl))
                    CoverImage.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(album.CoverUrl));

                // 记历史 + 刷新收藏按钮状态
                await LibraryStore.AddHistoryAsync(Stub());
                UpdateFavButton(await LibraryStore.IsFavoriteAsync(album.Id));

                if (album.Series.Count > 0)
                    foreach (var s in album.Series) Chapters.Add(s);
                else // 单章专辑：把自己作为唯一一章
                    Chapters.Add(new JmSeries { Id = album.Id, Name = album.Name, Sort = "1" });
            }
            catch (Exception ex)
            {
                TitleText.Text = "请求失败：" + ex.Message;
            }
            finally
            {
                Busy.IsActive = false;
            }
        }

        private JmAlbumStub Stub()
            => new JmAlbumStub { Id = _album.Id, Name = _album.Name, Author = string.Join("/", _album.Author) };

        private void UpdateFavButton(bool isFav)
            => FavButton.Content = isFav ? "★ 已收藏" : "☆ 收藏";

        private async void Fav_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (_album == null) return;
            UpdateFavButton(await LibraryStore.ToggleFavoriteAsync(Stub()));
        }

        private void Back_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }

        private void ChapterList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is JmSeries s)
                Frame.Navigate(typeof(ReaderPage), new ReaderArgs
                {
                    AlbumId = _album?.Id,
                    AlbumName = _album?.Name,
                    ChapterId = s.Id,
                    ChapterName = string.IsNullOrEmpty(s.Name) ? _album?.Name : s.Name,
                });
        }
    }
}
