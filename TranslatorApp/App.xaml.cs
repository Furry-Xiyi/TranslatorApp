using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;

namespace TranslatorApp
{
    public partial class App : Application
    {
        public static MainWindow? MainWindow { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();

            ApplySavedTheme();
            ApplySavedBackdrop();

            MainWindow.Activate();
        }

        private void ApplySavedTheme()
        {
            var themeValue = ApplicationData.Current.LocalSettings.Values["AppTheme"] as string ?? "Default";
            var theme = themeValue switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            if (MainWindow?.Content is FrameworkElement fe)
            {
                fe.RequestedTheme = theme;
            }
        }

        private void ApplySavedBackdrop()
        {
            var tag = ApplicationData.Current.LocalSettings.Values["BackdropMaterial"] as string ?? "Mica";

            try
            {
                MainWindow!.SystemBackdrop = tag switch
                {
                    "None" => null,
                    "MicaAlt" => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                    "Acrylic" => new DesktopAcrylicBackdrop(),
                    _ => new MicaBackdrop { Kind = MicaKind.Base }
                };
            }
            catch
            {
                MainWindow!.SystemBackdrop = null;
            }
        }
    }
}