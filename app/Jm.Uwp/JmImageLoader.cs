using System;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Jm.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Jm.Uwp
{
    /// <summary>下载单张图片，WebP 用 Universal.WebP 组件解码(系统无 codec)，必要时按 JmImageScramble 还原乱序。</summary>
    public static class JmImageLoader
    {
        private static readonly HttpClient Http = new HttpClient(
            new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });

        public static async Task<ImageSource> LoadAsync(string url, int photoId, string filename)
        {
            var bytes = await Http.GetByteArrayAsync(url);
            int num = JmImageScramble.GetSegmentCount(photoId, JmImageUrl.NameWithoutExt(filename));

            int w, h;
            byte[] bgra;
            if (WebpDecode.IsWebp(bytes))
            {
                var dec = await WebpDecode.DecodeBgraAsync(bytes);
                bgra = dec.Bgra; w = dec.Width; h = dec.Height;
            }
            else
            {
                // 非 webp(jpg/png)：系统 BitmapDecoder
                using (var stream = new InMemoryRandomAccessStream())
                {
                    await stream.WriteAsync(bytes.AsBuffer());
                    stream.Seek(0);
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    w = (int)decoder.PixelWidth;
                    h = (int)decoder.PixelHeight;
                    var px = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                        new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.DoNotColorManage);
                    bgra = px.DetachPixelData();
                }
            }

            byte[] pixels = bgra;
            if (num > 1)
            {
                int stride = w * 4;
                pixels = new byte[bgra.Length];
                foreach (var s in JmImageScramble.GetStrips(num, h))
                    Array.Copy(bgra, s.SrcY * stride, pixels, s.DstY * stride, s.Height * stride);
            }

            var wb = new WriteableBitmap(w, h);
            using (var ws = wb.PixelBuffer.AsStream())
                await ws.WriteAsync(pixels, 0, pixels.Length);
            wb.Invalidate();
            return wb;
        }
    }
}
