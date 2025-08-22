using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TranslatorApp.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.SpeechSynthesis;

namespace TranslatorApp.Pages;

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
        _player = new MediaPlayerElement
        {
            AutoPlay = true
        };

        _infoTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _infoTimer.Interval = TimeSpan.FromSeconds(2);
        _infoTimer.Tick += (_, __) =>
        {
            CopyInfoBar.IsOpen = false;
            _infoTimer?.Stop();
        };

        UpdateBindings();
    }

    private void OnlineTranslatePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _synth?.Dispose();
        _synth = null;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void UpdateBindings()
    {
        Bindings.Update(); // 更新 HasOutput / CanTranslate
    }

    private void BtnSwap_Click(object sender, RoutedEventArgs e)
    {
        // 互换语言（忽略“自动检测”情况）
        var inItem = CbInputLang.SelectedItem as ComboBoxItem;
        var outItem = CbOutputLang.SelectedItem as ComboBoxItem;

        if (inItem is null || outItem is null) return;

        if ((inItem.Tag?.ToString() ?? "auto") != "auto")
        {
            var inIndex = CbInputLang.SelectedIndex;
            var outIndex = CbOutputLang.SelectedIndex;
            CbInputLang.SelectedIndex = outIndex;
            CbOutputLang.SelectedIndex = inIndex;
        }
    }

    private void TbInput_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        UpdateBindings();
    }

    private void ShowCopyInfo()
    {
        CopyInfoBar.IsOpen = true; // 顶部下滑打开
        _infoTimer?.Stop();
        _infoTimer?.Start();       // 停留后自动收起
    }

    private void CopyInput_Click(object sender, RoutedEventArgs e)
    {
        var dp = new DataPackage();
        dp.SetText(TbInput.Text ?? string.Empty);
        Clipboard.SetContent(dp);
        ShowCopyInfo();
    }

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        var dp = new DataPackage();
        dp.SetText(TbOutput.Text ?? string.Empty);
        Clipboard.SetContent(dp);
        ShowCopyInfo();
    }

    private async void SpeakOutput_Click(object sender, RoutedEventArgs e)
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

    private void FavOutput_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TbOutput.Text))
        {
            FavoritesService.Add(TbOutput.Text.Trim());
        }
    }

    private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TbInput.Text))
            return;

        var api = (CbApi.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Bing";

        // 检查对应 API 是否已填写
        bool hasKey = api switch
        {
            "Bing" => !string.IsNullOrWhiteSpace(SettingsService.BingApiKey),
            "Baidu" => !string.IsNullOrWhiteSpace(SettingsService.BaiduApiKey) &&
                       !string.IsNullOrWhiteSpace(SettingsService.BaiduAppId),
            "Youdao" => !string.IsNullOrWhiteSpace(SettingsService.YoudaoApiKey) &&
                        !string.IsNullOrWhiteSpace(SettingsService.YoudaoAppKey),
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
                // 跳转设置页
                (Window.Current as MainWindow)?.Activate();
                Frame.Navigate(typeof(SettingsPage));
            }
            return;
        }

        // 读取参数
        var sourceLang = ((CbInputLang.SelectedItem as ComboBoxItem)?.Tag?.ToString()) ?? "auto";
        var targetLang = ((CbOutputLang.SelectedItem as ComboBoxItem)?.Tag?.ToString()) ?? "zh";
        var text = TbInput.Text?.Trim() ?? string.Empty;

        // 若目标语言与源语言相同，且非自动检测，直接回显
        if (!string.IsNullOrEmpty(text) && sourceLang != "auto" && sourceLang == targetLang)
        {
            TbOutput.Text = text;
            UpdateBindings();
            return;
        }

        // 准备调用
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
            UpdateBindings();
        }
        catch (OperationCanceledException)
        {
            // 用户中断，无需提示
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
        CbInputLang.IsEnabled = !isBusy;
        CbOutputLang.IsEnabled = !isBusy;
        TbInput.IsEnabled = !isBusy;

        BtnTranslate.Content = isBusy ? $"翻译中…（{apiLabel}）" : "翻译";
    }
}