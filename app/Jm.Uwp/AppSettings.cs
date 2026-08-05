using Windows.Storage;

namespace Jm.Uwp
{
    /// <summary>应用设置，存 LocalSettings（键值）。</summary>
    public static class AppSettings
    {
        private static ApplicationDataContainer S => ApplicationData.Current.LocalSettings;

        /// <summary>图片并发加载数（1~16，默认 4）。越大越快，但太大可能被服务器限流。</summary>
        public static int Concurrency
        {
            get
            {
                var v = S.Values["concurrency"];
                return v is int i && i >= 1 && i <= 16 ? i : 4;
            }
            set { S.Values["concurrency"] = value < 1 ? 1 : (value > 16 ? 16 : value); }
        }
    }
}
