using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DayZ.MaskEditor.App.Services;
using DayZ.MaskEditor.App.ViewModels;
using DayZ.MaskEditor.App.Views;

namespace DayZ.MaskEditor.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = SettingsService.Load();
            var vm = new MainViewModel(settings);
            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.ShutdownRequested += (_, _) => settings.Save();

            // Background update check (no-op for dev/portable runs).
            _ = UpdateService.CheckAndApplyAsync(vm.LogLine);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
