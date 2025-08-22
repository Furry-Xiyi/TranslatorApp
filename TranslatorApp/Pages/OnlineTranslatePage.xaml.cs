using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TranslatorApp.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.SpeechSynthesis;

namespace TranslatorApp.Pages
{
    public sealed partial class OnlineTranslatePage : Page
    {
        private SpeechSynthesizer? _synth;
        private MediaPlayerElement? _player;

        public bool HasOutput => !string.IsNullOrWhiteSpace(TbOutput?.Text);
        public bool CanTranslate => !string.IsNullOrWhiteSpace(TbInput?.Text);

        private DispatcherQueueTimer? _infoTimer;
        private CancellationTokenSource? _cts;

        public OnlineTranslatePage()
        {
            InitializeComponent();
            Loaded += OnlineTranslatePage_Loaded;
            Unloaded += OnlineTranslatePage_Unloaded;
        }

        private void OnlineTranslatePage_Loaded(object sender, RoutedEventArgs e)
        {
            _synth = new SpeechSynthesizer();
            _player = new MediaPlayerElement { AutoPlay = true };

            _infoTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _infoTimer.Interval = TimeSpan.FromSeconds(2);
            _infoTimer.Tick += (_, __) =>
            {
                ToastPanel.Opacity = 0;
                _infoTimer?.Stop();
            };

            LoadLanguageOptions();
            LoadApiOptions();
        }

        private void OnlineTranslatePage_Unloaded(object sender, RoutedEventArgs e)
        {
            _synth?.Dispose();
            _synth = null;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void LoadLanguageOptions()
        {
            string[] langs = { "自动检测", "中文", "英文", "日文", "韩文" };
            foreach (var lang in langs)
            {
                CbFromLang.Items.Add(new ComboBoxItem { Content = lang, Tag = GetLangTag(lang) });
                CbToLang.Items.Add(new ComboBoxItem { Content = lang, Tag = GetLangTag(lang) });
            }
            CbFromLang.SelectedIndex = 0;
            CbToLang.SelectedIndex = 1;
        }

        private string GetLangTag(string lang) => lang switch
        {
            "自动检测" => "auto",
            "中文" => "zh",
            "英文" => "en",
            "日文" => "ja",
            "韩文" => "ko",
            _ => "auto"
        };

        private void LoadApiOptions()
        {
            CbApi.Items.Add(new ComboBoxItem { Content = "Bing", Tag = "Bing" });
            CbApi.Items.Add(new ComboBoxItem { Content = "百度", Tag = "Baidu" });
            CbApi.Items.Add(new ComboBoxItem { Content = "有道", Tag = "Youdao" });

            var lastApi = SettingsService.LastUsedApi;
            if (!string.IsNullOrEmpty(lastApi))
            {
                foreach (ComboBoxItem item in CbApi.Items)
                {
                    if ((item.Tag?.ToString() ?? "") == lastApi)
                    {
                        CbApi.SelectedItem = item;
                        break;
                    }
                }
            }
            if (CbApi.SelectedItem == null)
                CbApi.SelectedIndex = 0;

            CbApi.SelectionChanged += (s, e) =>
            {
                SettingsService.LastUsedApi = (CbApi.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            };
        }

        private void BtnSwapLang_Click(object sender, RoutedEventArgs e)
        {
            // 无条件交换
            var inIndex = CbFromLang.SelectedIndex;
            var outIndex = CbToLang.SelectedIndex;
            CbFromLang.SelectedIndex = outIndex;
            CbToLang.SelectedIndex = inIndex;
        }

        private void TbInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 输入变化时同步到输出框
            TbOutput.Text = TbInput.Text;
        }

        private void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastPanel.Opacity = 1;
            _infoTimer?.Stop();
            _infoTimer?.Start();
        }

        private void BtnCopyInput_Click(object sender, RoutedEventArgs e)
        {
            var dp = new DataPackage();
            dp.SetText(TbInput.Text ?? string.Empty);
            Clipboard.SetContent(dp);
            ShowToast("已复制输入文本");
        }

        private void BtnCopyOutput_Click(object sender, RoutedEventArgs e)
        {
            var dp = new DataPackage();
            dp.SetText(TbOutput.Text ?? string.Empty);
            Clipboard.SetContent(dp);
            ShowToast("已复制翻译结果");
        }

        private async void BtnSpeakOutput_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbOutput.Text) || _synth is null || _player is null) return;
            try
            {
                var stream = await _synth.SynthesizeTextToStreamAsync(TbOutput.Text);
                _player.MediaPlayer.SetStreamSource(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void BtnFavOutput_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TbOutput.Text))
            {
                FavoritesService.Add(TbOutput.Text.Trim());
                ShowToast("已添加到收藏");
            }
        }

        private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbInput.Text))
                return;

            var api = (CbApi.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Bing";

            bool hasKey = api switch
            {
                "Bing" => !string.IsNullOrWhiteSpace(SettingsService.BingApiKey),
                "Baidu" => !string.IsNullOrWhiteSpace(SettingsService.BaiduAppId) &&
                           !string.IsNullOrWhiteSpace(SettingsService.BaiduSecret),
                "Youdao" => !string.IsNullOrWhiteSpace(SettingsService.YoudaoAppKey) &&
                            !string.IsNullOrWhiteSpace(SettingsService.YoudaoSecret),
                _ => false
            };

            if (!hasKey)
            {
                var dlg = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = $"{api} API 未填写",
                    Content = new TextBlock { Text = "请前往设置页填写 API 密钥。" },
                    PrimaryButtonText = "去设置",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary
                };
                var r = await dlg.ShowAsync();
                if (r == ContentDialogResult.Primary)
                {
                    Frame.Navigate(typeof(SettingsPage));
                }
                return;
            }

            var sourceLang = ((CbFromLang.SelectedItem as ComboBoxItem)?.Tag?.ToString()) ?? "auto";
            var targetLang = ((CbToLang.SelectedItem as ComboBoxItem)?.Tag?.ToString()) ?? "zh";
            var text = TbInput.Text?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(text) && sourceLang != "auto" && sourceLang == targetLang)
            {
                TbOutput.Text = text;
                return;
            }

            SetBusy(true, api);

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                var result = await TranslationService.TranslateAsync(
                    provider: api,
                    text: text,
                    from: sourceLang,
                    to: targetLang,
                    cancellationToken: _cts.Token);

                TbOutput.Text = result;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "翻译失败",
                    Content = new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = ex.Message,
                            TextWrapping = TextWrapping.Wrap
                        }
                    },
                    CloseButtonText = "确定"
                }.ShowAsync();
            }
            finally
            {
                SetBusy(false, api);
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void SetBusy(bool isBusy, string apiLabel)
        {
            BtnTranslate.IsEnabled = !isBusy;
            CbApi.IsEnabled = !isBusy;
            CbFromLang.IsEnabled = !isBusy;
            CbToLang.IsEnabled = !isBusy;
            TbInput.IsEnabled = !isBusy;

            BtnTranslate.Content = isBusy ? $"翻译中…（{apiLabel}）" : "翻译";
        }
    }
}