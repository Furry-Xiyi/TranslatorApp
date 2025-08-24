using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading.Tasks;
using TranslatorApp.Services;
using Windows.ApplicationModel;
using Windows.Storage;

namespace TranslatorApp.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isInitializing = true; // ��ʼ����־
        private string? _lastBackdropTag;    // ��¼��һ�α�������

        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            _isInitializing = false; // ��ʼ�����
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
            _lastBackdropTag = backdropValue;
            RbBackdrop.SelectedIndex = backdropValue switch
            {
                "Mica" => 0,
                "MicaAlt" => 1,
                "Acrylic" => 2,
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
                TxtVersion.Text = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";

                // �Զ���ȡӦ��ͼ��
                var logoUri = Package.Current.Logo;
                ImgAppIcon.Source = new BitmapImage(logoUri);
            }
            catch
            {
                TxtAppName.Text = "δ֪";
                TxtVersion.Text = "�汾��: ��ȡʧ��";
            }
        }

        private void RbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var theme = RbTheme.SelectedIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            ApplicationData.Current.LocalSettings.Values["AppTheme"] = theme.ToString();

            if (App.MainWindow?.Content is FrameworkElement fe)
            {
                fe.RequestedTheme = theme;
            }
        }

        private void RbBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var tag = (RbBackdrop.SelectedItem as RadioButton)?.Tag?.ToString() ?? "Mica";

            // ���ֵû�䣬ֱ�ӷ��أ�������˸
            if (tag == _lastBackdropTag) return;
            _lastBackdropTag = tag;

            ApplicationData.Current.LocalSettings.Values["BackdropMaterial"] = tag;

            if (App.MainWindow is not null)
            {
                try
                {
                    App.MainWindow.SystemBackdrop = tag switch
                    {
                        "MicaAlt" => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                        "Acrylic" => new DesktopAcrylicBackdrop(),
                        _ => new MicaBackdrop { Kind = MicaKind.Base }
                    };
                }
                catch
                {
                    App.MainWindow.SystemBackdrop = null;
                }
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
            // ���� MainWindow ��ȫ������
            App.MainWindow?.ShowLoadingOverlay();

            switch (apiName)
            {
                case "Bing":
                    SettingsService.BingAppId = key1;
                    SettingsService.BingSecret = key2;
                    SettingsService.BingApiKey = key2;
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

            // ����ȫ������
            App.MainWindow?.HideLoadingOverlay();

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