using Microsoft.Graphics.Canvas;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading;
using System.Threading.Tasks;
using TranslatorApp.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using WinRT;
using WinRT.Interop;

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
                HideToast();
                _infoTimer?.Stop();
            };

            LoadLanguageOptions();
            LoadApiOptions();
            BtnTranslate.IsEnabled = CanTranslate; // 初始化按钮状态
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
            if (CbApi == null) return;

            CbApi.Items.Clear();
            CbApi.Items.Add(new ComboBoxItem { Content = "Bing", Tag = "Bing" });
            CbApi.Items.Add(new ComboBoxItem { Content = "百度", Tag = "Baidu" });
            CbApi.Items.Add(new ComboBoxItem { Content = "有道", Tag = "Youdao" });

            var lastApi = SettingsService.LastUsedApi;
            if (!string.IsNullOrEmpty(lastApi))
            {
                foreach (ComboBoxItem item in CbApi.Items)
                {
                    if ((item.Tag as string) == lastApi)
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
                if (CbApi.SelectedItem is ComboBoxItem selected)
                    SettingsService.LastUsedApi = selected.Tag as string ?? "Bing";
            };
        }

        private void BtnSwapLang_Click(object sender, RoutedEventArgs e)
        {
            var inIndex = CbFromLang.SelectedIndex;
            var outIndex = CbToLang.SelectedIndex;
            CbFromLang.SelectedIndex = outIndex;
            CbToLang.SelectedIndex = inIndex;

            var rotate = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(rotate, SwapRotate);
            Storyboard.SetTargetProperty(rotate, "Angle");

            var sb = new Storyboard();
            sb.Children.Add(rotate);
            sb.Begin();
        }

        private void TbInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            BtnTranslate.IsEnabled = !string.IsNullOrWhiteSpace(TbInput.Text);
            if (string.IsNullOrWhiteSpace(TbInput.Text))
            {
                TbOutput.Text = string.Empty;
            }
        }

        private async void ShowToast(string message)
        {
            if (TopInfoBar.IsOpen)
            {
                var tcs = new TaskCompletionSource<bool>();

                var slideOut = new DoubleAnimation
                {
                    To = -80,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                Storyboard.SetTarget(slideOut, TopInfoBar.RenderTransform);
                Storyboard.SetTargetProperty(slideOut, "Y");

                var sbOut = new Storyboard();
                sbOut.Children.Add(slideOut);
                sbOut.Completed += (_, __) =>
                {
                    TopInfoBar.IsOpen = false;
                    tcs.TrySetResult(true);
                };
                sbOut.Begin();

                await tcs.Task;
                await Task.Delay(50);
            }

            TopInfoBar.Message = message;
            TopInfoBar.IsOpen = true;

            var slideIn = new DoubleAnimation
            {
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(slideIn, TopInfoBar.RenderTransform);
            Storyboard.SetTargetProperty(slideIn, "Y");

            var sbIn = new Storyboard();
            sbIn.Children.Add(slideIn);
            sbIn.Begin();

            _infoTimer?.Stop();
            _infoTimer?.Start();
        }

        private void HideToast()
        {
            var slideOut = new DoubleAnimation
            {
                To = -80,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slideOut, TopInfoBar.RenderTransform);
            Storyboard.SetTargetProperty(slideOut, "Y");

            var sb = new Storyboard();
            sb.Children.Add(slideOut);
            sb.Completed += (_, __) => TopInfoBar.IsOpen = false;
            sb.Begin();
        }

        private async void BtnCopyInput_Click(object sender, RoutedEventArgs e)
        {
            var dp = new DataPackage();
            dp.SetText(TbInput.Text ?? string.Empty);
            Clipboard.SetContent(dp);
            ShowToast("已复制输入文本");
            await Task.Delay(300);
        }

        private async void BtnCopyOutput_Click(object sender, RoutedEventArgs e)
        {
            var dp = new DataPackage();
            dp.SetText(TbOutput.Text ?? string.Empty);
            Clipboard.SetContent(dp);
            ShowToast("已复制翻译结果");
            await Task.Delay(300);
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
                    await Task.Delay(200);
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
                // 用户取消翻译，不做处理
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

        private SpeechRecognizer? _continuousRecognizer;
        private bool _isListening = false;
        private bool _isMicBusy = false; // 防抖锁

        private async void BtnMicInput_Click(object sender, RoutedEventArgs e)
        {
            if (_isMicBusy) return;

            try
            {
                if (!_isListening)
                {
                    _isMicBusy = true; // 锁定启动过程

                    if (SpeechRecognizer.SystemSpeechLanguage == null)
                    {
                        await new ContentDialog
                        {
                            XamlRoot = this.XamlRoot,
                            Title = "语音识别未启用",
                            Content = new TextBlock
                            {
                                Text = "请先在 Windows 设置 → 隐私和安全 → 语音 中开启“联机语音识别”并接受隐私政策。",
                                TextWrapping = TextWrapping.Wrap
                            },
                            PrimaryButtonText = "去设置",
                            CloseButtonText = "稍后",
                            DefaultButton = ContentDialogButton.Primary
                        }.ShowAsync();
                        return;
                    }

                    _continuousRecognizer = new SpeechRecognizer();
                    await _continuousRecognizer.CompileConstraintsAsync();

                    _continuousRecognizer.ContinuousRecognitionSession.ResultGenerated += (s, args) =>
                    {
                        var recognized = args.Result.Text;
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            TbInput.Text = recognized;
                        });
                    };

                    await _continuousRecognizer.ContinuousRecognitionSession.StartAsync();

                    ShowToast("请说...");
                    BtnMicInput.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
                    _isListening = true;
                }
                else
                {
                    _isMicBusy = true; // 锁定停止过程

                    if (_continuousRecognizer != null)
                    {
                        await _continuousRecognizer.ContinuousRecognitionSession.StopAsync();
                        _continuousRecognizer.Dispose();
                        _continuousRecognizer = null;
                    }

                    ShowToast("已停止录音");
                    BtnMicInput.ClearValue(Button.StyleProperty);
                    _isListening = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"麦克风操作异常: {ex}");
                ShowToast("语音识别出错");

                // 出错时强制恢复状态
                BtnMicInput.ClearValue(Button.StyleProperty);
                _isListening = false;
            }
            finally
            {
                _isMicBusy = false; // 只在启动/停止完成后解锁
            }
        }

        private void SetBusy(bool isBusy, string apiLabel)
        {
            BtnTranslate.IsEnabled = !isBusy && !string.IsNullOrWhiteSpace(TbInput.Text);
            CbApi.IsEnabled = !isBusy;
            CbFromLang.IsEnabled = !isBusy;
            CbToLang.IsEnabled = !isBusy;
            TbInput.IsEnabled = !isBusy;

            BtnTranslate.Content = isBusy ? $"翻译中…（{apiLabel}）" : "翻译";
        }

        // ====== Win2D 截图 + OCR ======
        private async Task<SoftwareBitmap?> CaptureScreenAsync()
        {
            var picker = new GraphicsCapturePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            var item = await picker.PickSingleItemAsync();
            if (item == null) return null;

            // 用 Win2D 获取共享设备并转成 WinRT IDirect3DDevice
            var canvasDevice = CanvasDevice.GetSharedDevice();
            var d3dDevice = canvasDevice.As<IDirect3DDevice>();

            using var framePool = Direct3D11CaptureFramePool.Create(
                d3dDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                item.Size);
            using var session = framePool.CreateCaptureSession(item);

            var tcs = new TaskCompletionSource<SoftwareBitmap?>();

            framePool.FrameArrived += (s, e) =>
            {
                using var frame = s.TryGetNextFrame();
                var bitmap = SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface).AsTask().Result;
                tcs.TrySetResult(bitmap);
                session.Dispose();
                framePool.Dispose();
            };

            session.StartCapture();
            return await tcs.Task;
        }

        private async Task<string> RunOcrAsync(SoftwareBitmap bitmap)
        {
            var engine = OcrEngine.TryCreateFromLanguage(new Language("zh-CN"))
                         ?? OcrEngine.TryCreateFromUserProfileLanguages();

            if (engine is null) return string.Empty;

            var result = await engine.RecognizeAsync(bitmap);
            return result?.Text ?? string.Empty;
        }

        private async void BtnOcrCapture_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bitmap = await CaptureScreenAsync();
                if (bitmap == null)
                {
                    ShowToast("未选择截图区域");
                    return;
                }

                var text = await RunOcrAsync(bitmap);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    TbInput.Text = text;
                    BtnTranslate.IsEnabled = true;
                    ShowToast("OCR识别完成");
                }
                else
                {
                    ShowToast("未识别到文字");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR异常: {ex}");
                ShowToast("OCR识别出错");
            }
        }
    }
}