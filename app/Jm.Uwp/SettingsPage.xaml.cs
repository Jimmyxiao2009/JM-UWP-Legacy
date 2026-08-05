using System;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace Jm.Uwp
{
    public sealed partial class SettingsPage : Page
    {
        private bool _ready;

        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ConcurrencySlider.Value = AppSettings.Concurrency;
            ConcurrencyValue.Text = $"当前：{AppSettings.Concurrency} 线程";
            _ready = true;

            DomainText.Text = $"当前 {App.Jm.Domains.Count} 个：" + string.Join("、", App.Jm.Domains);

            var v = Package.Current.Id.Version;
            VersionText.Text = $"JM Reader  v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }

        private void Concurrency_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_ready) return;
            int n = (int)Math.Round(e.NewValue);
            AppSettings.Concurrency = n;
            ConcurrencyValue.Text = $"当前：{n} 线程";
        }

        private async void RefreshDomain_Click(object sender, RoutedEventArgs e)
        {
            RefreshDomainButton.IsEnabled = false;
            DomainText.Text = "正在从发布页获取…";
            bool ok = await App.Jm.RefreshApiDomainsAsync();
            DomainText.Text = ok
                ? $"已更新 {App.Jm.Domains.Count} 个：" + string.Join("、", App.Jm.Domains)
                : "更新失败（网络不通或被拦截）";
            RefreshDomainButton.IsEnabled = true;
        }

        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            await LibraryStore.ClearHistoryAsync();
            await new ContentDialog { Title = "已清空浏览历史", PrimaryButtonText = "好" }.ShowAsync();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
