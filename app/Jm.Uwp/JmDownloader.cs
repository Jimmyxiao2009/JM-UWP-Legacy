using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Jm.Core;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Jm.Uwp
{
    /// <summary>把章节图片下载到本地（乱序的会先还原再存）。统一存成 {名}.jpg，目录 LocalState\downloads\{photoId}\。</summary>
    public static class JmDownloader
    {
        private static readonly HttpClient Http = new HttpClient(
            new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });

        /// <summary>下载整章并登记到下载清单，返回清单记录。progress 报告已完成张数。</summary>
        public static async Task<DownloadEntry> DownloadChapterAsync(
            int photoId, IList<string> images,
            string albumId, string albumName, string chapterName,
            IProgress<int> progress = null)
        {
            var root = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "downloads", CreationCollisionOption.OpenIfExists);
            var folder = await root.CreateFolderAsync(photoId.ToString(), CreationCollisionOption.OpenIfExists);

            // 按设置的并发数同时下载
            string domain = null; // 首个成功的域名，后续复用（写竞争无害）
            int done = 0;
            var sem = new SemaphoreSlim(AppSettings.Concurrency);
            var tasks = new List<Task>(images.Count);
            foreach (var file in images)
            {
                var f = file;
                tasks.Add(DownloadSlotAsync(sem, folder, photoId, f,
                    () => domain, d => { if (d != null) domain = d; },
                    () => progress?.Report(Interlocked.Increment(ref done))));
            }
            await Task.WhenAll(tasks);

            var entry = new DownloadEntry
            {
                AlbumId = string.IsNullOrEmpty(albumId) ? photoId.ToString() : albumId,
                AlbumName = albumName ?? chapterName,
                ChapterId = photoId.ToString(),
                ChapterName = chapterName ?? $"章节 {photoId}",
                PageCount = images.Count,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                ThumbFile = images.Count > 0 ? JmImageUrl.NameWithoutExt(images[0]) + ".jpg" : null,
            };
            await DownloadStore.UpsertAsync(entry);
            return entry;
        }

        private static async Task DownloadSlotAsync(SemaphoreSlim sem, StorageFolder folder, int photoId, string file,
            Func<string> getDomain, Action<string> setDomain, Action onDone)
        {
            await sem.WaitAsync();
            try
            {
                var used = await SaveOneAsync(folder, photoId, file, getDomain());
                setDomain(used);
            }
            finally
            {
                sem.Release();
                onDone();
            }
        }

        /// <summary>
        /// 下载、（webp/乱序的）解码还原后，统一保存为 {名}.jpg 到 folder。
        /// preferred 非空时只试这个域名，否则逐个候选域名。返回成功使用的域名（失败返回 null）。
        /// 阅读器在线缓存也复用这个方法（folder 传 temp 目录即可）。
        /// </summary>
        public static async Task<string> SaveOneAsync(StorageFolder folder, int photoId, string file, string preferred)
        {
            var outName = JmImageUrl.NameWithoutExt(file) + ".jpg";
            var candidates = preferred != null ? new[] { preferred } : JmImageUrl.DefaultImageDomains;
            foreach (var d in candidates)
            {
                try
                {
                    var url = JmImageUrl.Build(d, photoId.ToString(), file);
                    var bytes = await Http.GetByteArrayAsync(url);
                    int num = JmImageScramble.GetSegmentCount(photoId, JmImageUrl.NameWithoutExt(file));

                    if (num <= 1 && !WebpDecode.IsWebp(bytes))
                    {
                        // 不乱序且非 webp（多为老专辑的 jpg）：原样存成 {名}.jpg
                        var raw = await folder.CreateFileAsync(outName, CreationCollisionOption.ReplaceExisting);
                        await FileIO.WriteBytesAsync(raw, bytes);
                        return d;
                    }

                    // 解码到 BGRA（webp 用 Universal.WebP，其它用系统解码器）
                    int w, h;
                    byte[] src;
                    if (WebpDecode.IsWebp(bytes))
                    {
                        var dec = await WebpDecode.DecodeBgraAsync(bytes);
                        src = dec.Bgra; w = dec.Width; h = dec.Height;
                    }
                    else
                    {
                        using (var stream = new InMemoryRandomAccessStream())
                        {
                            await stream.WriteAsync(bytes.AsBuffer());
                            stream.Seek(0);
                            var decoder = await BitmapDecoder.CreateAsync(stream);
                            w = (int)decoder.PixelWidth;
                            h = (int)decoder.PixelHeight;
                            var px = await decoder.GetPixelDataAsync(
                                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                                new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation,
                                ColorManagementMode.DoNotColorManage);
                            src = px.DetachPixelData();
                        }
                    }

                    int stride = w * 4;
                    byte[] dst = src;
                    if (num > 1)
                    {
                        dst = new byte[src.Length];
                        foreach (var s in JmImageScramble.GetStrips(num, h))
                            Array.Copy(src, s.SrcY * stride, dst, s.DstY * stride, s.Height * stride);
                    }

                    var outFile = await folder.CreateFileAsync(outName, CreationCollisionOption.ReplaceExisting);
                    using (var outStream = await outFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outStream);
                        enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                            (uint)w, (uint)h, 96, 96, dst);
                        await enc.FlushAsync();
                    }
                    return d;
                }
                catch
                {
                    // 换下一个域名
                }
            }
            return null;
        }
    }
}
