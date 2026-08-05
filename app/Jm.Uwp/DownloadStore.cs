using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jm.Core;
using Newtonsoft.Json;
using Windows.Storage;

namespace Jm.Uwp
{
    /// <summary>一条已下载章节的清单记录。图片本体在 LocalState\downloads\{ChapterId}\ 下，存为 {名}.jpg。</summary>
    public sealed class DownloadEntry
    {
        public string AlbumId { get; set; }
        public string AlbumName { get; set; }
        public string ChapterId { get; set; }   // = photoId
        public string ChapterName { get; set; }
        public int PageCount { get; set; }
        public string SavedAt { get; set; }
        /// <summary>第一页的输出文件名（用于离线封面缩略图）。</summary>
        public string ThumbFile { get; set; }

        /// <summary>离线封面地址（指向本地已下载的第一页）。</summary>
        [JsonIgnore]
        public string Thumb => string.IsNullOrEmpty(ChapterId) || string.IsNullOrEmpty(ThumbFile)
            ? null
            : $"ms-appdata:///local/downloads/{ChapterId}/{ThumbFile}";

        [JsonIgnore] public string Display => string.IsNullOrEmpty(ChapterName) ? $"章节 {ChapterId}" : ChapterName;
        [JsonIgnore] public string SubText => $"JM{AlbumId}  {PageCount}页  {SavedAt}";
    }

    /// <summary>下载清单存储：LocalState\downloads_index.json。</summary>
    public static class DownloadStore
    {
        private const string IndexFile = "downloads_index.json";

        public static async Task<List<DownloadEntry>> LoadAsync()
        {
            try
            {
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(IndexFile);
                if (item is StorageFile file)
                {
                    var text = await FileIO.ReadTextAsync(file);
                    return JsonConvert.DeserializeObject<List<DownloadEntry>>(text) ?? new List<DownloadEntry>();
                }
            }
            catch { /* 损坏当空 */ }
            return new List<DownloadEntry>();
        }

        private static async Task SaveAsync(List<DownloadEntry> list)
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                IndexFile, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, JsonConvert.SerializeObject(list));
        }

        public static async Task<bool> ContainsAsync(string chapterId)
            => (await LoadAsync()).Exists(x => x.ChapterId == chapterId);

        /// <summary>新增或更新一条记录（按 ChapterId 去重、置顶）。</summary>
        public static async Task UpsertAsync(DownloadEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ChapterId)) return;
            var list = await LoadAsync();
            list.RemoveAll(x => x.ChapterId == entry.ChapterId);
            list.Insert(0, entry);
            await SaveAsync(list);
        }

        /// <summary>删除一条记录并连同其本地图片目录一起删掉。</summary>
        public static async Task RemoveAsync(string chapterId)
        {
            var list = await LoadAsync();
            list.RemoveAll(x => x.ChapterId == chapterId);
            await SaveAsync(list);
            try
            {
                var root = await ApplicationData.Current.LocalFolder.TryGetItemAsync("downloads");
                if (root is StorageFolder rf)
                {
                    var dir = await rf.TryGetItemAsync(chapterId);
                    if (dir is StorageFolder df) await df.DeleteAsync();
                }
            }
            catch { /* 目录不在就算了 */ }
        }
    }
}
