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
}