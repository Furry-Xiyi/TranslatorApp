using Microsoft.UI.Xaml;
using Windows.Storage;

namespace TranslatorApp.Services;

public static class SettingsService
{
    private static readonly ApplicationDataContainer Local = ApplicationData.Current.LocalSettings;

    // 主题
    public static ElementTheme AppTheme
    {
        get
        {
            var v = (string?)Local.Values[nameof(AppTheme)] ?? "Default";
            return v switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
        set
        {
            Local.Values[nameof(AppTheme)] = value switch
            {
                ElementTheme.Light => "Light",
                ElementTheme.Dark => "Dark",
                _ => "Default"
            };
        }
    }

    // 背景材质: None/Mica/MicaAlt/Acrylic
    public static string Backdrop
    {
        get => (string?)Local.Values[nameof(Backdrop)] ?? "Mica";
        set => Local.Values[nameof(Backdrop)] = value;
    }

    // API Keys
    public static string BingApiKey
    {
        get => (string?)Local.Values[nameof(BingApiKey)] ?? string.Empty;
        set => Local.Values[nameof(BingApiKey)] = value;
    }

    public static string BaiduApiKey
    {
        get => (string?)Local.Values[nameof(BaiduApiKey)] ?? string.Empty;
        set => Local.Values[nameof(BaiduApiKey)] = value;
    }

    // 新增：Baidu AppId（用于与你的 OnlineTranslatePage 检查匹配）
    public static string BaiduAppId
    {
        get => (string?)Local.Values[nameof(BaiduAppId)] ?? string.Empty;
        set => Local.Values[nameof(BaiduAppId)] = value;
    }

    public static string YoudaoApiKey
    {
        get => (string?)Local.Values[nameof(YoudaoApiKey)] ?? string.Empty;
        set => Local.Values[nameof(YoudaoApiKey)] = value;
    }

    // 新增：Youdao AppKey（用于与你的 OnlineTranslatePage 检查匹配）
    public static string YoudaoAppKey
    {
        get => (string?)Local.Values[nameof(YoudaoAppKey)] ?? string.Empty;
        set => Local.Values[nameof(YoudaoAppKey)] = value;
    }

    // 查词页所选网站：Google/Baidu/Youdao
    public static string LastLookupSite
    {
        get => (string?)Local.Values[nameof(LastLookupSite)] ?? "Youdao";
        set => Local.Values[nameof(LastLookupSite)] = value;
    }

    public static bool HasAnyApiKey() =>
        !string.IsNullOrWhiteSpace(BingApiKey)
        || !string.IsNullOrWhiteSpace(BaiduApiKey)
        || !string.IsNullOrWhiteSpace(YoudaoApiKey);
}