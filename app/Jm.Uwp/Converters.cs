using System;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace Jm.Uwp
{
    /// <summary>把图片 URL 字符串转成 BitmapImage，供 Image.Source 绑定（封面等）。</summary>
    public sealed class UrlToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var url = value as string;
            if (string.IsNullOrEmpty(url)) return null;
            return new BitmapImage(new Uri(url));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
