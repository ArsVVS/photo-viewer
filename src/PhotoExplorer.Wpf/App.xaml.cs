using System.Windows;
using PhotoExplorer.Core;

namespace PhotoExplorer.Wpf;

public partial class App : Application
{
    // Настройки и кеш общие для всего приложения
    public static AppSettings Settings { get; private set; } = new();
    public static ThumbnailCache Cache { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Settings = AppSettings.Load();
        Cache = new ThumbnailCache();

        var window = new MainWindow();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Запоминаем последнюю папку и прочие настройки
        Settings.Save();
        base.OnExit(e);
    }
}
