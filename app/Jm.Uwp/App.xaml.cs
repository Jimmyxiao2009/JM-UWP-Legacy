using Jm.Core;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Jm.Uwp
{
    public sealed partial class App : Application
    {
        /// <summary>全 App 共用一个客户端，这样动态拉到的最新域名能跨页面复用。</summary>
        public static JmClient Jm { get; } = new JmClient();

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();
                rootFrame.Navigated += RootFrame_Navigated;
                Window.Current.Content = rootFrame;
                // 统一处理“返回”：手机硬件返回键 / 标题栏返回箭头都走这里，
                // 之前没接管，按硬件返回键会直接退到系统主页。
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            }

            if (rootFrame.Content == null)
                rootFrame.Navigate(typeof(MainPage), e.Arguments);

            Window.Current.Activate();
            UpdateBackButton(rootFrame);
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
            => UpdateBackButton(sender as Frame);

        private static void UpdateBackButton(Frame frame)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                frame != null && frame.CanGoBack
                    ? AppViewBackButtonVisibility.Visible
                    : AppViewBackButtonVisibility.Collapsed;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (e.Handled) return;
            var handler = (Window.Current.Content as Frame)?.Content as IBackHandler;
            if (handler != null && handler.HandleBack()) { e.Handled = true; return; }
            if (Window.Current.Content is Frame frame && frame.CanGoBack)
            {
                e.Handled = true;   // App 内返回上一页，而不是退出到系统主页
                frame.GoBack();
            }
        }
    }

    public interface IBackHandler
    {
        bool HandleBack();
    }
}
