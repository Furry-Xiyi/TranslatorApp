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
            // 主题
            var themeValue = ApplicationData.Current.LocalSettings.Values["AppTheme"] as string ?? "Default";
            RbTheme.SelectedIndex = themeValue switch
            {
                "Light" => 1,
                "Dark" => 2,
                _ => 0
            };

            // 背景材质
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

            // 应用信息
            try
            {
                TxtAppName.Text = Package.Current.DisplayName;
                var v = Package.Current.Id.Version;
                TxtVersion.Text = $"版本号: {v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
            catch
            {
                TxtAppName.Text = "未知";
                TxtVersion.Text = "版本号: 获取失败";
            }

            // 启动时应用一次背景材质，确保与设置一致
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

            // 即时保存
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
                            // 以字符串解析的方式尝试使用 Alternative/Alt，避免编译期依赖具体枚举成员
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
                        // 默认 Mica（Base）
                        App.MainAppWindow.SystemBackdrop = new MicaBackdrop();
                        break;
                }
            }
            catch
            {
                // 某些设备/系统版本不支持时，回退为无材质
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
            // 显示全局加载动画
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;

            // 保存到 SettingsService
            switch (apiName)
            {
                case "Bing":
                    SettingsService.BingAppId = key1;
                    SettingsService.BingSecret = key2;
                    SettingsService.BingApiKey = key2; // 兼容旧逻辑
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

            // 隐藏加载动画
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