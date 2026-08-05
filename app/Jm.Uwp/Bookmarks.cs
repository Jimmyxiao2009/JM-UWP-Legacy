using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Windows.Storage;

namespace Jm.Uwp
{
    /// <summary>一条书签：记录到“某专辑某章节的第几页”。</summary>
    public sealed class Bookmark
    {
        public string AlbumId { get; set; }
        public string AlbumName { get; set; }
        public string ChapterId { get; set; }
        public string ChapterName { get; set; }
        public int Page { get; set; }
        public string AddedAt { get; set; }

        public string Display => $"{ChapterName}  第{Page + 1}页";
        public string SubText => $"JM{AlbumId}  {AddedAt}";
    }

    /// <summary>书签存储：LocalState\bookmarks.json。</summary>
    public static class BookmarkStore
    {
        private const string FileName = "bookmarks.json";

        public static async Task<List<Bookmark>> LoadAsync()
        {
            try
            {
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileName);
                if (item is StorageFile file)
                {
                    var text = await FileIO.ReadTextAsync(file);
                    return JsonConvert.DeserializeObject<List<Bookmark>>(text) ?? new List<Bookmark>();
                }
            }
            catch { /* 文件损坏就当空 */ }
            return new List<Bookmark>();
        }

        public static async Task SaveAsync(List<Bookmark> list)
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                FileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, JsonConvert.SerializeObject(list));
        }

        public static async Task AddAsync(Bookmark b)
        {
            var list = await LoadAsync();
            list.Insert(0, b);
            await SaveAsync(list);
        }

        public static async Task RemoveAsync(Bookmark b)
        {
            var list = await LoadAsync();
            list.RemoveAll(x => x.AlbumId == b.AlbumId && x.ChapterId == b.ChapterId && x.Page == b.Page);
            await SaveAsync(list);
        }
    }
}
