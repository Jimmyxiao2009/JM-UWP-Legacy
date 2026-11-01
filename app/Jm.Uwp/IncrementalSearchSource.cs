using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Jm.Core;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Jm.Uwp
{
    /// <summary>
    /// 支持滚动到底自动加载下一页的搜索结果集合。
    /// GridView/ListView 绑定它后，会在需要更多项时自动调用 LoadMoreItemsAsync。
    /// </summary>
    public sealed class IncrementalSearchSource : ObservableCollection<JmAlbumStub>, ISupportIncrementalLoading
    {
        private readonly JmClient _client;
        private readonly string _query;
        private readonly string _orderBy;
        private readonly string _category;
        private int _nextPage = 1;
        private int _total = -1;
        private bool _loading;
        private bool _noMore;

        /// <summary>状态回调（搜索中/加载更多/共N条/错误），供页面显示。</summary>
        public Action<string> StatusChanged;

        public IncrementalSearchSource(JmClient client, string query, string orderBy = null, string category = null)
        {
            _client = client;
            _query = query;
            _orderBy = orderBy;
            _category = category;
        }

        public bool HasMoreItems => !_noMore;

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            => System.Runtime.InteropServices.WindowsRuntime.AsyncInfo.Run(_ => LoadMoreAsync());

        private async Task<LoadMoreItemsResult> LoadMoreAsync()
        {
            if (_loading || _noMore) return new LoadMoreItemsResult { Count = 0 };
            _loading = true;
            try
            {
                StatusChanged?.Invoke(Count == 0 ? "搜索中…" : "加载更多…");

                JmApiResult r;
                try
                {
                    r = await _client.SearchAsync(_query, _nextPage, _orderBy, _category);
                }
                catch (JmException)
                {
                    // 域名都连不上 → 从发布页刷新后重试一次
                    if (!await _client.RefreshApiDomainsAsync())
                    {
                        _noMore = true;
                        StatusChanged?.Invoke("网络不通或被拦截（可能需要代理/TUN）");
                        return new LoadMoreItemsResult { Count = 0 };
                    }
                    r = await _client.SearchAsync(_query, _nextPage, _orderBy, _category);
                }

                if (!r.IsOk)
                {
                    _noMore = true;
                    StatusChanged?.Invoke($"接口返回异常：code={r.Code} {r.ErrorMsg}");
                    return new LoadMoreItemsResult { Count = 0 };
                }

                var page = r.AsSearchPage();
                if (_total < 0) int.TryParse(page.Total, out _total);

                uint added = 0;
                foreach (var item in page.Content)
                {
                    Add(item);
                    added++;
                }
                _nextPage++;

                // 本页空 或 已加载到总数 → 没有更多了
                if (page.Content.Count == 0 || (_total >= 0 && Count >= _total))
                    _noMore = true;

                StatusChanged?.Invoke(Count == 0
                    ? "没有结果"
                    : $"共 {_total} 条，已加载 {Count}" + (_noMore ? "（到底了）" : ""));
                return new LoadMoreItemsResult { Count = added };
            }
            catch (Exception ex)
            {
                _noMore = true;
                StatusChanged?.Invoke("加载失败：" + ex.Message);
                return new LoadMoreItemsResult { Count = 0 };
            }
            finally
            {
                _loading = false;
            }
        }
    }
}
