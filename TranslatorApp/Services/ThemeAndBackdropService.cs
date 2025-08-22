using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using TranslatorApp.Services;
using WinRT; // 关键：提供 CastExtensions.As<T>

namespace TranslatorApp.Services;

public static class ThemeAndBackdropService
{
    private static SystemBackdropConfiguration? _configuration;
    private static MicaController? _micaController;
    private static DesktopAcrylicController? _acrylicController;

    public static void ApplyThemeFromSettings(Window window)
    {
        if (window.Content is FrameworkElement fe)
        {
            fe.RequestedTheme = SettingsService.AppTheme;
        }
    }

    public static void ApplyBackdropFromSettings(Window window)
    {
        DisposeBackdrop();

        var type = SettingsService.Backdrop; // None/Mica/MicaAlt/Acrylic
        if (type == "None") return;

        _configuration ??= new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default
        };

        if (window.Content is FrameworkElement fe)
        {
            _configuration.Theme = fe.ActualTheme switch
            {
                ElementTheme.Dark => SystemBackdropTheme.Dark,
                ElementTheme.Light => SystemBackdropTheme.Light,
                _ => SystemBackdropTheme.Default
            };
        }

        // 修复：使用 WinRT.CastExtensions.As<T>(window) 获取 ICompositionSupportsSystemBackdrop
        var target = CastExtensions.As<ICompositionSupportsSystemBackdrop>(window);

        switch (type)
        {
            case "Mica":
            case "MicaAlt":
                _micaController = new MicaController
                {
                    Kind = type == "MicaAlt" ? MicaKind.BaseAlt : MicaKind.Base
                };
                _micaController.AddSystemBackdropTarget(target);
                _micaController.SetSystemBackdropConfiguration(_configuration);
                break;

            case "Acrylic":
                _acrylicController = new DesktopAcrylicController();
                _acrylicController.AddSystemBackdropTarget(target);
                _acrylicController.SetSystemBackdropConfiguration(_configuration);
                break;
        }
    }

    private static void DisposeBackdrop()
    {
        _micaController?.Dispose();
        _micaController = null;
        _acrylicController?.Dispose();
        _acrylicController = null;
    }
}