using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jm.Core;
using Newtonsoft.Json;
using Windows.Storage;

namespace Jm.Uwp
{
    /// <summary>收藏 / 历史的本地存储（LocalState\favorites.json、history.json）。条目复用 JmAlbumStub。</summary>
    public static class LibraryStore
    {
        private const string FavFile = "favorites.json";
        private const string HistFile = "history.json";
        private const int HistoryCap = 100;

        private static async Task<List<JmAlbumStub>> LoadAsync(string name)
        {
            try
            {
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(name);
                if (item is StorageFile file)
                {
                    var text = await FileIO.ReadTextAsync(file);
                    return JsonConvert.DeserializeObject<List<JmAlbumStub>>(text) ?? new List<JmAlbumStub>();
                }
            }
            catch { /* 损坏当空 */ }
            return new List<JmAlbumStub>();
        }

        private static async Task SaveAsync(string name, List<JmAlbumStub> list)
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                name, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, JsonConvert.SerializeObject(list));
        }

        public static Task<List<JmAlbumStub>> LoadFavoritesAsync() => LoadAsync(FavFile);
        public static Task<List<JmAlbumStub>> LoadHistoryAsync() => LoadAsync(HistFile);

        public static async Task<bool> IsFavoriteAsync(string id)
            => (await LoadAsync(FavFile)).Exists(x => x.Id == id);

        /// <summary>收藏/取消收藏，返回切换后的状态（true=已收藏）。</summary>
        public static async Task<bool> ToggleFavoriteAsync(JmAlbumStub a)
        {
            var list = await LoadAsync(FavFile);
            int i = list.FindIndex(x => x.Id == a.Id);
            bool nowFav;
            if (i >= 0) { list.RemoveAt(i); nowFav = false; }
            else { list.Insert(0, a); nowFav = true; }
            await SaveAsync(FavFile, list);
            return nowFav;
        }

        /// <summary>记一条浏览历史：按 id 去重、置顶、限量。</summary>
        public static async Task AddHistoryAsync(JmAlbumStub a)
        {
            if (a == null || string.IsNullOrEmpty(a.Id)) return;
            var list = await LoadAsync(HistFile);
            list.RemoveAll(x => x.Id == a.Id);
            list.Insert(0, a);
            if (list.Count > HistoryCap)
                list.RemoveRange(HistoryCap, list.Count - HistoryCap);
            await SaveAsync(HistFile, list);
        }

        public static Task ClearHistoryAsync() => SaveAsync(HistFile, new List<JmAlbumStub>());
    }
}
