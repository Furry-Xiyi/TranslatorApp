using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using TranslatorApp.Services;
using Windows.ApplicationModel;

namespace TranslatorApp.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // 主题
            RbTheme.SelectedIndex = SettingsService.AppTheme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };

            // 背景
            foreach (ComboBoxItem item in CbBackdrop.Items)
            {
                if ((item.Tag?.ToString() ?? "Mica") == SettingsService.Backdrop)
                {
                    CbBackdrop.SelectedItem = item;
                    break;
                }
            }

            // API Keys（合并存储）
            TbBing.Text = SettingsService.BingApiKey;
            TbBaidu.Text = SettingsService.BaiduApiKey;   // 格式：AppId|SecretKey
            TbYoudao.Text = SettingsService.YoudaoApiKey; // 格式：AppKey|SecretKey

            // 应用信息
            try
            {
                TxtAppName.Text = Package.Current.DisplayName;
                var v = Package.Current.Id.Version;
                TxtVersion.Text = $"版本号：{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
            catch
            {
                TxtAppName.Text = "未知";
                TxtVersion.Text = "版本号：未能获取";
            }
        }

        private void RbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingsService.AppTheme = RbTheme.SelectedIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            if (App.MainAppWindow?.Content is FrameworkElement fe)
            {
                fe.RequestedTheme = SettingsService.AppTheme;
            }
        }

        private void CbBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbBackdrop.SelectedItem is ComboBoxItem item)
            {
                SettingsService.Backdrop = item.Tag?.ToString() ?? "Mica";
                if (App.MainAppWindow is not null)
                {
                    ThemeAndBackdropService.ApplyBackdropFromSettings(App.MainAppWindow);
                }
            }
        }

        // 保存 Bing API
        private async void SaveBing_Click(object sender, RoutedEventArgs e)
        {
            await SaveApiKeyAsync("Bing", TbBing.Text?.Trim() ?? string.Empty);
        }

        // 保存 Baidu API（合并存储）
        private async void SaveBaidu_Click(object sender, RoutedEventArgs e)
        {
            await SaveApiKeyAsync("Baidu", TbBaidu.Text?.Trim() ?? string.Empty);
        }

        // 保存 Youdao API（合并存储）
        private async void SaveYoudao_Click(object sender, RoutedEventArgs e)
        {
            await SaveApiKeyAsync("Youdao", TbYoudao.Text?.Trim() ?? string.Empty);
        }

        private async Task SaveApiKeyAsync(string apiName, string key)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;

            // 临时保存到 SettingsService 以便验证
            switch (apiName)
            {
                case "Bing":
                    SettingsService.BingApiKey = key;
                    break;
                case "Baidu":
                    SettingsService.BaiduApiKey = key; // 格式：AppId|SecretKey
                    break;
                case "Youdao":
                    SettingsService.YoudaoApiKey = key; // 格式：AppKey|SecretKey
                    break;
            }

            string verifyResult;
            try
            {
                verifyResult = await TranslationService.TranslateAsync(apiName, "Hello", "en", "zh");
            }
            catch
            {
                verifyResult = string.Empty;
            }

            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = false;

            if (string.IsNullOrWhiteSpace(verifyResult) ||
                verifyResult.Contains("失败") ||
                verifyResult.Contains("异常") ||
                verifyResult.Contains("无效"))
            {
                await new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = $"{apiName} API Key 验证失败",
                    Content = new TextBlock
                    {
                        Text = $"无法使用提供的 {apiName} API Key 进行翻译，请检查 Key 是否正确。",
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = "确定"
                }.ShowAsync();
                return;
            }

            // 验证成功后保存
            switch (apiName)
            {
                case "Bing":
                    SettingsService.BingApiKey = key;
                    break;
                case "Baidu":
                    SettingsService.BaiduApiKey = key;
                    break;
                case "Youdao":
                    SettingsService.YoudaoApiKey = key;
                    break;
            }

            await new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "保存成功",
                Content = new TextBlock
                {
                    Text = $"{apiName} API Key 已验证并保存。",
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = "确定"
            }.ShowAsync();
        }
    }
}