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
            // ����
            RbTheme.SelectedIndex = SettingsService.AppTheme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };

            // ����
            foreach (ComboBoxItem item in CbBackdrop.Items)
            {
                if ((item.Tag?.ToString() ?? "Mica") == SettingsService.Backdrop)
                {
                    CbBackdrop.SelectedItem = item;
                    break;
                }
            }

            // API Keys���ϲ��洢��
            TbBing.Text = SettingsService.BingApiKey;
            TbBaidu.Text = SettingsService.BaiduApiKey;   // ��ʽ��AppId|SecretKey
            TbYoudao.Text = SettingsService.YoudaoApiKey; // ��ʽ��AppKey|SecretKey

            // Ӧ����Ϣ
            try
            {
                TxtAppName.Text = Package.Current.DisplayName;
                var v = Package.Current.Id.Version;
                TxtVersion.Text = $"�汾�ţ�{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
            catch
            {
                TxtAppName.Text = "δ֪";
                TxtVersion.Text = "�汾�ţ�δ�ܻ�ȡ";
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

        // ���� Bing API
        private async void SaveBing_Click(object sender, RoutedEventArgs e)
        {
            await SaveApiKeyAsync("Bing", TbBing.Text?.Trim() ?? string.Empty);
        }

        // ���� Baidu API���ϲ��洢��
        private async void SaveBaidu_Click(object sender, RoutedEventArgs e)
        {
            await SaveApiKeyAsync("Baidu", TbBaidu.Text?.Trim() ?? string.Empty);
        }

        // ���� Youdao API���ϲ��洢��
        private async void SaveYoudao_Click(object sender, RoutedEventArgs e)
        {
            await SaveApiKeyAsync("Youdao", TbYoudao.Text?.Trim() ?? string.Empty);
        }

        private async Task SaveApiKeyAsync(string apiName, string key)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;

            // ��ʱ���浽 SettingsService �Ա���֤
            switch (apiName)
            {
                case "Bing":
                    SettingsService.BingApiKey = key;
                    break;
                case "Baidu":
                    SettingsService.BaiduApiKey = key; // ��ʽ��AppId|SecretKey
                    break;
                case "Youdao":
                    SettingsService.YoudaoApiKey = key; // ��ʽ��AppKey|SecretKey
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
                verifyResult.Contains("ʧ��") ||
                verifyResult.Contains("�쳣") ||
                verifyResult.Contains("��Ч"))
            {
                await new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = $"{apiName} API Key ��֤ʧ��",
                    Content = new TextBlock
                    {
                        Text = $"�޷�ʹ���ṩ�� {apiName} API Key ���з��룬���� Key �Ƿ���ȷ��",
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = "ȷ��"
                }.ShowAsync();
                return;
            }

            // ��֤�ɹ��󱣴�
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
                Title = "����ɹ�",
                Content = new TextBlock
                {
                    Text = $"{apiName} API Key ����֤�����档",
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = "ȷ��"
            }.ShowAsync();
        }
    }
}