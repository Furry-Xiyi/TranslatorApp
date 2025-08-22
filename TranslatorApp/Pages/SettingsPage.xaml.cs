using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using TranslatorApp.Services;
using Windows.ApplicationModel;
using Windows.Storage;

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
            var themeValue = ApplicationData.Current.LocalSettings.Values["AppTheme"] as string ?? "Default";
            RbTheme.SelectedIndex = themeValue switch
            {
                "Light" => 1,
                "Dark" => 2,
                _ => 0
            };

            // ��������
            var backdropValue = ApplicationData.Current.LocalSettings.Values["BackdropMaterial"] as string ?? "Mica";
            RbBackdrop.SelectedIndex = backdropValue switch
            {
                "None" => 0,
                "Mica" => 1,
                "MicaAlt" => 2,
                "Acrylic" => 3,
                _ => 1
            };

            // API Keys
            TbBingAppId.Text = SettingsService.BingAppId;
            TbBingSecret.Text = SettingsService.BingSecret;
            TbBaiduAppId.Text = SettingsService.BaiduAppId;
            TbBaiduSecret.Text = SettingsService.BaiduSecret;
            TbYoudaoAppKey.Text = SettingsService.YoudaoAppKey;
            TbYoudaoSecret.Text = SettingsService.YoudaoSecret;

            // Ӧ����Ϣ
            try
            {
                TxtAppName.Text = Package.Current.DisplayName;
                var v = Package.Current.Id.Version;
                TxtVersion.Text = $"�汾��: {v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
            catch
            {
                TxtAppName.Text = "δ֪";
                TxtVersion.Text = "�汾��: ��ȡʧ��";
            }

            // ����ʱӦ��һ�α������ʣ�ȷ��������һ��
            ApplyBackdropToWindow(backdropValue);
        }

        private void RbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var theme = RbTheme.SelectedIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            // ��ʱ����
            ApplicationData.Current.LocalSettings.Values["AppTheme"] = theme.ToString();

            if (App.MainAppWindow?.Content is FrameworkElement fe)
            {
                fe.RequestedTheme = theme;
            }
        }

        private void RbBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tag = (RbBackdrop.SelectedItem as RadioButton)?.Tag?.ToString() ?? "Mica";
            ApplicationData.Current.LocalSettings.Values["BackdropMaterial"] = tag;

            ApplyBackdropToWindow(tag);
        }

        private static void ApplyBackdropToWindow(string tag)
        {
            if (App.MainAppWindow is null) return;

            try
            {
                switch (tag)
                {
                    case "None":
                        App.MainAppWindow.SystemBackdrop = null;
                        break;

                    case "Acrylic":
                        App.MainAppWindow.SystemBackdrop = new DesktopAcrylicBackdrop();
                        break;

                    case "MicaAlt":
                        {
                            // ���ַ��������ķ�ʽ����ʹ�� Alternative/Alt�������������������ö�ٳ�Ա
                            var mica = new MicaBackdrop();
                            if (Enum.TryParse<MicaKind>("Alternative", out var altKind))
                            {
                                mica.Kind = altKind;
                            }
                            else if (Enum.TryParse<MicaKind>("Alt", out var altKindLegacy))
                            {
                                mica.Kind = altKindLegacy;
                            }
                            App.MainAppWindow.SystemBackdrop = mica;
                            break;
                        }

                    default:
                        // Ĭ�� Mica��Base��
                        App.MainAppWindow.SystemBackdrop = new MicaBackdrop();
                        break;
                }
            }
            catch
            {
                // ĳЩ�豸/ϵͳ�汾��֧��ʱ������Ϊ�޲���
                App.MainAppWindow.SystemBackdrop = null;
            }
        }

        private async void SaveBing_Click(object sender, RoutedEventArgs e)
        {
            await SaveApiKeyAsync("Bing",
                TbBingAppId.Text?.Trim() ?? string.Empty,
                TbBingSecret.Text?.Trim() ?? string.Empty);
        }

        private async void SaveBaidu_Click(object sender, RoutedEventArgs e)
        {
            await SaveApiKeyAsync("Baidu",
                TbBaiduAppId.Text?.Trim() ?? string.Empty,
                TbBaiduSecret.Text?.Trim() ?? string.Empty);
        }

        private async void SaveYoudao_Click(object sender, RoutedEventArgs e)
        {
            await SaveApiKeyAsync("Youdao",
                TbYoudaoAppKey.Text?.Trim() ?? string.Empty,
                TbYoudaoSecret.Text?.Trim() ?? string.Empty);
        }

        private async Task SaveApiKeyAsync(string apiName, string key1, string key2 = "")
        {
            // ��ʾȫ�ּ��ض���
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;

            // ���浽 SettingsService
            switch (apiName)
            {
                case "Bing":
                    SettingsService.BingAppId = key1;
                    SettingsService.BingSecret = key2;
                    SettingsService.BingApiKey = key2; // ���ݾ��߼�
                    break;
                case "Baidu":
                    SettingsService.BaiduAppId = key1;
                    SettingsService.BaiduSecret = key2;
                    break;
                case "Youdao":
                    SettingsService.YoudaoAppKey = key1;
                    SettingsService.YoudaoSecret = key2;
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

            // ���ؼ��ض���
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