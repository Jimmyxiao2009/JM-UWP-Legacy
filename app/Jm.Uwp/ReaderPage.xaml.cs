using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Jm.Core;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Jm.Uwp
{
    /// <summary>进阅读器的参数：带上专辑/章节信息，方便书签和标题显示。Offline=true 走本地已下载图片。</summary>
    public sealed class ReaderArgs
    {
        public string AlbumId { get; set; }
        public string AlbumName { get; set; }
        public string ChapterId { get; set; }
        public string ChapterName { get; set; }
        public int JumpToPage { get; set; } // 从书签进来时跳到的页(0 基)
        public bool Offline { get; set; }   // true=读本地下载，不联网
    }

    /// <summary>阅读器里的一页。图片按需加载、滚出视图即释放，避免一次性解码全章导致内存爆炸闪退。</summary>
    public sealed class ReaderPageItem : INotifyPropertyChanged
    {
        public int Index { get; }
        public string File { get; }     // 在线:原始文件名(webp); 离线:jpg 文件名
        public string OutName { get; }  // 落地文件名 {名}.jpg
        public bool Requested;          // 是否已发起加载(被回收时重置)

        public ReaderPageItem(int index, string file, string outName)
        {
            Index = index; File = file; OutName = outName;
        }

        private ImageSource _image;
        public ImageSource Image
        {
            get => _image;
            set { _image = value; Raise(nameof(Image)); Raise(nameof(SpinnerVisibility)); }
        }

        public Visibility SpinnerVisibility => _image == null ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public sealed partial class ReaderPage : Page
    {
        private readonly JmClient _client = App.Jm;
        private string _imageDomain;
        private ReaderArgs _args;
        private JmChapter _chapter;
        private int _photoId;
        private bool _offline;
        private int _currentPage;
        private int _decodeWidth;
        private bool _suppressSlider;
        private ScrollViewer _scroll;
        private StorageFolder _imageFolder; // 在线:temp\reader\{id}; 离线:downloads\{id}
        private readonly SemaphoreSlim _loadSem = new SemaphoreSlim(Math.Max(1, AppSettings.Concurrency));

        public ObservableCollection<ReaderPageItem> Pages { get; } = new ObservableCollection<ReaderPageItem>();

        public ReaderPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is ReaderArgs ra)
                _args = ra;
            else if (e.Parameter is string s)
                _args = new ReaderArgs { ChapterId = s };

            if (_args == null || !int.TryParse(_args.ChapterId, out _photoId))
            {
                ShowStatus("无效的章节 id");
                return;
            }

            _offline = _args.Offline;
            TitleText.Text = _args.ChapterName ?? $"章节 {_photoId}";
            DownloadButton.Visibility = _offline ? Visibility.Collapsed : Visibility.Visible;
            ComputeDecodeWidth();

            try
            {
                List<string> files;
                if (_offline)
                {
                    _imageFolder = await OpenOfflineFolderAsync();
                    if (_imageFolder == null) { ShowStatus("本地下载文件夹不存在"); return; }
                    files = await ListOfflineFilesAsync(_imageFolder);
                    if (files.Count == 0) { ShowStatus("没有已下载的图片"); return; }
                    BuildToc(files.Count);
                    for (int i = 0; i < files.Count; i++)
                        Pages.Add(new ReaderPageItem(i, files[i], files[i]));
                }
                else
                {
                    var result = await _client.GetChapterAsync(_photoId);
                    if (!result.IsOk)
                    {
                        ShowStatus($"加载失败：code={result.Code} {result.ErrorMsg}");
                        return;
                    }
                    _chapter = result.AsChapter();
                    files = _chapter.Images;
                    if (files.Count == 0) { ShowStatus("本章没有图片"); return; }
                    _imageFolder = await OpenTempFolderAsync();
                    BuildToc(files.Count);
                    for (int i = 0; i < files.Count; i++)
                        Pages.Add(new ReaderPageItem(i, files[i], JmImageUrl.NameWithoutExt(files[i]) + ".jpg"));
                }

                // 占位已就绪：可见的几页会通过 ContainerContentChanging 自动开始加载
                HideStatus();
                HookScrollViewer();

                if (_args.JumpToPage > 0 && _args.JumpToPage < Pages.Count)
                    JumpTo(_args.JumpToPage);
            }
            catch (Exception ex)
            {
                ShowStatus("请求失败：" + ex.Message);
            }
        }

        // ---- 按需加载 / 回收 ----

        private void PageHost_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!(args.Item is ReaderPageItem item)) return;
            if (args.InRecycleQueue)
            {
                // 滚出视图：释放解码后的位图，回收内存
                item.Image = null;
                item.Requested = false;
            }
            else if (!item.Requested)
            {
                // 进入视图：异步加载（不阻塞 UI 线程）
                item.Requested = true;
                var _ = LoadPageAsync(item);
            }
        }

        private async Task LoadPageAsync(ReaderPageItem item)
        {
            try
            {
                if (!_offline)
                {
                    // 在线：确保 temp 缓存里有这张（没有就下载+解码+还原成 jpg）
                    var existing = await _imageFolder.TryGetItemAsync(item.OutName);
                    if (existing == null)
                    {
                        await _loadSem.WaitAsync();
                        try
                        {
                            var used = await JmDownloader.SaveOneAsync(_imageFolder, _photoId, item.File, _imageDomain);
                            if (used == null) return; // 域名全失败：留着转圈，下次滚到再试
                            _imageDomain = used;
                        }
                        finally { _loadSem.Release(); }
                    }
                }

                var bmp = new BitmapImage();
                bmp.DecodePixelType = DecodePixelType.Physical;
                if (_decodeWidth > 0) bmp.DecodePixelWidth = _decodeWidth; // 解码到屏幕宽即可，省内存
                bmp.UriSource = PageUri(item);
                item.Image = bmp;
            }
            catch
            {
                item.Requested = false; // 失败下次再试
            }
        }

        private Uri PageUri(ReaderPageItem item)
            => _offline
                ? new Uri($"ms-appdata:///local/downloads/{_photoId}/{item.OutName}")
                : new Uri($"ms-appdata:///temp/reader/{_photoId}/{item.OutName}");

        private void ComputeDecodeWidth()
        {
            try
            {
                double scale = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
                int px = (int)Math.Round(Window.Current.Bounds.Width * scale);
                _decodeWidth = Math.Max(360, Math.Min(1080, px));
            }
            catch { _decodeWidth = 720; }
        }

        private async Task<StorageFolder> OpenOfflineFolderAsync()
        {
            var root = await ApplicationData.Current.LocalFolder.TryGetItemAsync("downloads") as StorageFolder;
            if (root == null) return null;
            return await root.TryGetItemAsync(_photoId.ToString()) as StorageFolder;
        }

        private async Task<StorageFolder> OpenTempFolderAsync()
        {
            var root = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(
                "reader", CreationCollisionOption.OpenIfExists);
            return await root.CreateFolderAsync(_photoId.ToString(), CreationCollisionOption.OpenIfExists);
        }

        private static async Task<List<string>> ListOfflineFilesAsync(StorageFolder folder)
        {
            var files = await folder.GetFilesAsync();
            var names = new List<string>();
            foreach (var f in files)
                if (f.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    names.Add(f.Name);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        // ---- 目录 / 进度条 / 跳页 ----

        private void BuildToc(int count)
        {
            var items = new List<string>(count);
            for (int i = 0; i < count; i++)
                items.Add($"第 {i + 1} 页");
            TocList.ItemsSource = items;

            PageSlider.Maximum = count;
            PageLabel.Text = $"1/{count}";
        }

        private void JumpTo(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= Pages.Count) return;
            _currentPage = pageIndex;
            PageHost.ScrollIntoView(Pages[pageIndex], ScrollIntoViewAlignment.Leading);
            _suppressSlider = true;
            PageSlider.Value = pageIndex + 1;
            _suppressSlider = false;
            PageLabel.Text = $"{pageIndex + 1}/{Pages.Count}";
        }

        private void PageSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressSlider) return;
            JumpTo((int)Math.Round(e.NewValue) - 1);
        }

        private void Toc_ItemClick(object sender, ItemClickEventArgs e)
        {
            int idx = TocList.Items.IndexOf(e.ClickedItem);
            if (idx >= 0) JumpTo(idx);
        }

        private void HookScrollViewer()
        {
            _scroll = FindDescendant<ScrollViewer>(PageHost);
            if (_scroll != null)
            {
                _scroll.MinZoomFactor = 1;
                _scroll.MaxZoomFactor = 5;
                _scroll.ViewChanged += Scroll_ViewChanged;
            }
        }

        private void Scroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_scroll == null || Pages.Count == 0 || _scroll.ScrollableHeight <= 0) return;
            double ratio = _scroll.VerticalOffset / _scroll.ScrollableHeight;
            int page = (int)Math.Round(ratio * (Pages.Count - 1));
            if (page < 0) page = 0;
            if (page >= Pages.Count) page = Pages.Count - 1;
            _currentPage = page;
            _suppressSlider = true;
            PageSlider.Value = page + 1;
            _suppressSlider = false;
            PageLabel.Text = $"{page + 1}/{Pages.Count}";
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) return t;
                var deeper = FindDescendant<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // ---- 书签 / 下载 ----

        private async void Bookmark_Click(object sender, RoutedEventArgs e)
        {
            await BookmarkStore.AddAsync(new Bookmark
            {
                AlbumId = _args.AlbumId ?? _args.ChapterId,
                AlbumName = _args.AlbumName ?? _args.ChapterName,
                ChapterId = _args.ChapterId,
                ChapterName = _args.ChapterName ?? $"章节 {_photoId}",
                Page = _currentPage,
                AddedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            });
            await new ContentDialog
            {
                Title = "已添加书签",
                Content = $"{_args.ChapterName ?? ""} 第{_currentPage + 1}页",
                PrimaryButtonText = "好",
            }.ShowAsync();
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (_offline || _chapter == null || _chapter.Images.Count == 0) return;
            StatusPanel.Visibility = Visibility.Visible;
            Busy.IsActive = true;
            int total = _chapter.Images.Count;
            var progress = new Progress<int>(done => StatusText.Text = $"下载中 {done}/{total}");
            try
            {
                await JmDownloader.DownloadChapterAsync(
                    _photoId, _chapter.Images,
                    _args.AlbumId, _args.AlbumName, _args.ChapterName ?? TitleText.Text, progress);
                StatusText.Text = "下载完成，可在主页“下载”里离线阅读";
                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                StatusText.Text = "下载失败：" + ex.Message;
                await Task.Delay(2000);
            }
            finally
            {
                HideStatus();
            }
        }

        // ---- 状态 ----

        private void ShowStatus(string text)
        {
            Busy.IsActive = false;
            StatusPanel.Visibility = Visibility.Visible;
            StatusText.Text = text;
        }

        private void HideStatus()
        {
            Busy.IsActive = false;
            StatusPanel.Visibility = Visibility.Collapsed;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
