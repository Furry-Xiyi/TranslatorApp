using Microsoft.UI.Xaml;
using TranslatorApp.Services;

namespace TranslatorApp;

public partial class App : Application
{
    public static Window? MainAppWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        MainAppWindow = window;

        // 应用主题与背景材质
        ThemeAndBackdropService.ApplyThemeFromSettings(window);
        ThemeAndBackdropService.ApplyBackdropFromSettings(window);

        window.Activate();
    }
}