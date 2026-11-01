using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Jm.Core;
using Microsoft.Phone.Controls;

namespace Jm.Wp81
{
    public partial class MainPage : PhoneApplicationPage
    {
        private readonly JmClient _client;
        private bool _initialized;
        private int _currentPage = 1;
        private bool _isLoading;

        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>
        {
            "全部", "同人志", "单行本", "短篇", "CG", "Cosplay"
        };

        public ObservableCollection<JmAlbumStub> SearchResults { get; } = new ObservableCollection<JmAlbumStub>();
        public ObservableCollection<JmAlbumStub> Favorites { get; } = new ObservableCollection<JmAlbumStub>();
        public ObservableCollection<JmAlbumStub> History { get; } = new ObservableCollection<JmAlbumStub>();

        public MainPage()
        {
            InitializeComponent();
            _client = new JmClient();

            DataContext = this;
            ResultList.ItemsSource = SearchResults;
            FavList.ItemsSource = Favorites;
            HistList.ItemsSource = History;

            CategoryList.ItemsSource = Categories;
            CategoryList.SelectedIndex = 0;
            OrderPicker.SelectedIndex = 0;

            _initialized = true;

            // 启动时后台拉取最新域名
            var _ = _client.RefreshApiDomainsAsync();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ReloadCurrentTabAsync();
        }

        private async void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ReloadCurrentTabAsync();
        }

        private async Task ReloadCurrentTabAsync()
        {
            int idx = MainPivot.SelectedIndex;
            if (idx == 1) // 收藏
            {
                Favorites.Clear();
                // TODO: 从本地存储加载收藏
                FavEmpty.Visibility = Favorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (idx == 2) // 历史
            {
                History.Clear();
                // TODO: 从本地存储加载历史
                HistEmpty.Visibility = History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.Focus(); // 关闭键盘
                DoSearch();
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            DoSearch();
        }

        private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            if (SearchResults.Count > 0) DoSearch();
        }

        private void OrderPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            if (SearchResults.Count > 0) DoSearch();
        }

        private async void DoSearch()
        {
            var query = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                StatusText.Text = "输入关键词开始搜索";
                StatusText.Visibility = Visibility.Visible;
                SearchResults.Clear();
                return;
            }

            if (_isLoading) return;
            _isLoading = true;
            _currentPage = 1;

            SearchResults.Clear();
            StatusText.Text = "搜索中...";
            StatusText.Visibility = Visibility.Visible;
            SearchProgress.Visibility = Visibility.Visible;

            try
            {
                var category = GetCategory();
                var orderBy = GetOrderBy();

                JmApiResult result;
                try
                {
                    result = await _client.SearchAsync(query, _currentPage, orderBy, category);
                }
                catch (JmException)
                {
                    // 域名连不上，尝试刷新域名后重试
                    if (!await _client.RefreshApiDomainsAsync())
                    {
                        StatusText.Text = "网络不通或被拦截（可能需要代理）";
                        return;
                    }
                    result = await _client.SearchAsync(query, _currentPage, orderBy, category);
                }

                if (!result.IsOk)
                {
                    StatusText.Text = $"接口返回异常: code={result.Code} {result.ErrorMsg}";
                    return;
                }

                var page = result.AsSearchPage();
                if (page.Content.Count == 0)
                {
                    StatusText.Text = "没有找到结果";
                    return;
                }

                foreach (var item in page.Content)
                {
                    SearchResults.Add(item);
                }

                StatusText.Visibility = Visibility.Collapsed;
                StatusText.Text = $"找到 {page.Total} 条结果";
            }
            catch (Exception ex)
            {
                StatusText.Text = "搜索失败: " + ex.Message;
            }
            finally
            {
                SearchProgress.Visibility = Visibility.Collapsed;
                _isLoading = false;
            }
        }

        private string GetCategory()
        {
            switch (CategoryList.SelectedIndex)
            {
                case 1: return JmCategory.Doujinshi;
                case 2: return JmCategory.Single;
                case 3: return JmCategory.Short;
                case 4: return JmCategory.CG;
                case 5: return JmCategory.Cosplay;
                default: return JmCategory.All;
            }
        }

        private string GetOrderBy()
        {
            switch (OrderPicker.SelectedIndex)
            {
                case 1: return JmSearchOrder.Latest;
                case 2: return JmSearchOrder.View;
                case 3: return JmSearchOrder.Like;
                case 4: return JmSearchOrder.Picture;
                default: return null; // 综合
            }
        }

        private void ResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultList.SelectedItem is JmAlbumStub stub)
            {
                // TODO: 导航到详情页
                // NavigationService.Navigate(new Uri("/AlbumPage.xaml?id=" + stub.Id, UriKind.Relative));
                ResultList.SelectedItem = null;
            }
        }

        private void FavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FavList.SelectedItem is JmAlbumStub stub)
            {
                // TODO: 导航到详情页
                FavList.SelectedItem = null;
            }
        }

        private void HistList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistList.SelectedItem is JmAlbumStub stub)
            {
                // TODO: 导航到详情页
                HistList.SelectedItem = null;
            }
        }

        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 清空历史记录存储
            History.Clear();
            HistEmpty.Visibility = Visibility.Visible;
        }

        private void Bookmarks_Click(object sender, EventArgs e)
        {
            // TODO: 导航到书签页
            // NavigationService.Navigate(new Uri("/BookmarksPage.xaml", UriKind.Relative));
        }

        private void Settings_Click(object sender, EventArgs e)
        {
            // TODO: 导航到设置页
            // NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }
    }
}
