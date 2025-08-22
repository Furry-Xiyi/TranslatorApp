using Windows.Storage;

namespace TranslatorApp.Services
{
    public static class SettingsService
    {
        private static readonly ApplicationDataContainer Local = ApplicationData.Current.LocalSettings;

        // Bing（双字段）
        public static string BingAppId
        {
            get => (string?)Local.Values[nameof(BingAppId)] ?? string.Empty;
            set => Local.Values[nameof(BingAppId)] = value;
        }

        public static string BingSecret
        {
            get => (string?)Local.Values[nameof(BingSecret)] ?? string.Empty;
            set => Local.Values[nameof(BingSecret)] = value;
        }

        // 兼容旧字段：如有旧代码仍读取 BingApiKey，这里保留
        public static string BingApiKey
        {
            get => (string?)Local.Values[nameof(BingApiKey)] ?? string.Empty;
            set => Local.Values[nameof(BingApiKey)] = value;
        }

        // Baidu（双字段）
        public static string BaiduAppId
        {
            get => (string?)Local.Values[nameof(BaiduAppId)] ?? string.Empty;
            set => Local.Values[nameof(BaiduAppId)] = value;
        }

        public static string BaiduSecret
        {
            get => (string?)Local.Values[nameof(BaiduSecret)] ?? string.Empty;
            set => Local.Values[nameof(BaiduSecret)] = value;
        }

        // Youdao（双字段）
        public static string YoudaoAppKey
        {
            get => (string?)Local.Values[nameof(YoudaoAppKey)] ?? string.Empty;
            set => Local.Values[nameof(YoudaoAppKey)] = value;
        }

        public static string YoudaoSecret
        {
            get => (string?)Local.Values[nameof(YoudaoSecret)] ?? string.Empty;
            set => Local.Values[nameof(YoudaoSecret)] = value;
        }

        // 最近查词站点（可选）
        public static string LastLookupSite
        {
            get => (string?)Local.Values[nameof(LastLookupSite)] ?? "Youdao";
            set => Local.Values[nameof(LastLookupSite)] = value;
        }

        // 新增：最近使用的翻译 API
        public static string LastUsedApi
        {
            get => (string?)Local.Values[nameof(LastUsedApi)] ?? string.Empty;
            set => Local.Values[nameof(LastUsedApi)] = value;
        }

        // 是否已配置任意一个 API
        public static bool HasAnyApiKey() =>
            !string.IsNullOrWhiteSpace(BingSecret) ||
            !string.IsNullOrWhiteSpace(BingApiKey) || // 兼容旧逻辑
            !string.IsNullOrWhiteSpace(BaiduAppId) ||
            !string.IsNullOrWhiteSpace(BaiduSecret) ||
            !string.IsNullOrWhiteSpace(YoudaoAppKey) ||
            !string.IsNullOrWhiteSpace(YoudaoSecret);
    }
}