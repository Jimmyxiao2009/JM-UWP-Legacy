using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Jm.Core;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Jm.Uwp
{
    public sealed partial class MainPage : Page, IBackHandler
    {
        private readonly JmClient _client = App.Jm;
        private bool _initialized;

        public ObservableCollection<JmAlbumStub> Favorites { get; } = new ObservableCollection<JmAlbumStub>();
        public ObservableCollection<JmAlbumStub> History { get; } = new ObservableCollection<JmAlbumStub>();
        public ObservableCollection<DownloadEntry> Downloads { get; } = new ObservableCollection<DownloadEntry>();

        public MainPage()
        {
            InitializeComponent();
            // Returning from a comic must restore its source tab and search results.
            NavigationCacheMode = NavigationCacheMode.Required;
            FavGrid.ItemsSource = Favorites;
            HistGrid.ItemsSource = History;
            DlGrid.ItemsSource = Downloads;
            _initialized = true;
            // 启动时后台静默拉一次最新域名（不阻塞 UI）
            var _ = _client.RefreshApiDomainsAsync();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // 从详情/阅读器返回时刷新当前所在分页（收藏/历史/下载可能有变化）
            await ReloadCurrentTabAsync();
        }

        private async void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => await ReloadCurrentTabAsync();

        public bool HandleBack()
        {
            if (AdvPanel.Visibility != Visibility.Visible) return false;
            AdvToggle.IsChecked = false;
            AdvPanel.Visibility = Visibility.Collapsed;
            return true;
        }

        private async System.Threading.Tasks.Task ReloadCurrentTabAsync()
        {
            int idx = RootPivot.SelectedIndex;
            if (idx == 1) // 收藏
            {
                Favorites.Clear();
                foreach (var a in await LibraryStore.LoadFavoritesAsync()) Favorites.Add(a);
                FavEmpty.Visibility = Favorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (idx == 2) // 历史
            {
                History.Clear();
                foreach (var a in await LibraryStore.LoadHistoryAsync()) History.Add(a);
                HistEmpty.Visibility = History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (idx == 3) // 下载
            {
                Downloads.Clear();
                foreach (var d in await DownloadStore.LoadAsync()) Downloads.Add(d);
                DlEmpty.Visibility = Downloads.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            await LibraryStore.ClearHistoryAsync();
            History.Clear();
            HistEmpty.Visibility = Visibility.Visible;
        }

        private void ViewToggle_Click(object sender, RoutedEventArgs e)
        {
            bool gridNow = ResultGrid.Visibility == Visibility.Visible;
            ResultGrid.Visibility = gridNow ? Visibility.Collapsed : Visibility.Visible;
            ResultList.Visibility = gridNow ? Visibility.Visible : Visibility.Collapsed;
            ViewToggle.Content = gridNow ? "网格" : "列表"; // 按钮显示“切换到的目标”
        }

        private void Bookmarks_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(BookmarksPage));

        private void Settings_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(SettingsPage));

        private void SearchButton_Click(object sender, RoutedEventArgs e)
            => DoSearch();

        private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                DoSearch();
        }

        private void AdvToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (AdvPanel == null) return;
            AdvPanel.Visibility = AdvToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OrderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 改了排序：若已有搜索结果，按新排序重搜一次
            if (!_initialized) return;
            if (ResultGrid.ItemsSource is IncrementalSearchSource) DoSearch();
        }

        private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 改了分类：若已有搜索结果，按新分类重搜一次
            if (!_initialized) return;
            if (ResultGrid.ItemsSource is IncrementalSearchSource) DoSearch();
        }

        private void DoSearch()
        {
            var query = BuildQuery();
            if (string.IsNullOrEmpty(query))
            {
                StatusText.Text = "输入关键词开始搜索";
                ResultGrid.ItemsSource = null;
                ResultList.ItemsSource = null;
                return;
            }

            // 增量加载集合：控件滚到底会自动调 LoadMoreItemsAsync 拉下一页
            var source = new IncrementalSearchSource(_client, query, GetOrderBy(), GetCategory())
            {
                StatusChanged = s => StatusText.Text = s
            };
            StatusText.Text = "搜索中…";
            ResultGrid.ItemsSource = source;
            ResultList.ItemsSource = source;
        }

        /// <summary>把关键词 + 包含标签 + 排除标签拼成服务器的 search_query（排除项加“-”前缀）。</summary>
        private string BuildQuery()
        {
            var parts = new List<string>();
            var kw = QueryBox.Text?.Trim();
            if (!string.IsNullOrEmpty(kw)) parts.Add(kw);
            AddTags(parts, IncludeBox.Text, false);
            AddTags(parts, ExcludeBox.Text, true);
            return string.Join(" ", parts);
        }

        private static void AddTags(List<string> parts, string text, bool exclude)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (var raw in text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                var tag = raw.Trim();
                if (tag.Length == 0) continue;
                if (exclude) parts.Add(tag.StartsWith("-") ? tag : "-" + tag);
                else parts.Add(tag);
            }
        }

        private string GetOrderBy()
        {
            switch (OrderBox.SelectedIndex)
            {
                case 1: return JmSearchOrder.Latest;
                case 2: return JmSearchOrder.View;
                case 3: return JmSearchOrder.Like;
                case 4: return JmSearchOrder.Picture;
                default: return null; // 综合
            }
        }

        private string GetCategory()
        {
            switch (CategoryBox.SelectedIndex)
            {
                case 1: return JmCategory.Doujinshi;
                case 2: return JmCategory.Single;
                case 3: return JmCategory.Short;
                case 4: return JmCategory.CG;
                case 5: return JmCategory.Cosplay;
                default: return JmCategory.All; // 全部
            }
        }

        private void Card_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is JmAlbumStub stub)
                Frame.Navigate(typeof(AlbumPage), stub.Id);
        }

        private void Download_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is DownloadEntry d)
                Frame.Navigate(typeof(ReaderPage), new ReaderArgs
                {
                    AlbumId = d.AlbumId,
                    AlbumName = d.AlbumName,
                    ChapterId = d.ChapterId,
                    ChapterName = d.ChapterName,
                    Offline = true,
                });
        }

        private async void DeleteDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string id)
            {
                await DownloadStore.RemoveAsync(id);
                await ReloadCurrentTabAsync();
            }
        }
    }
}
