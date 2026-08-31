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
        ApplyTheme();

        // Если кеш вырос больше заданного размера – удаляем давно не открывавшиеся миниатюры
        long maxBytes = Settings.MaxCacheSizeMb * 1024L * 1024L;
        Task.Run(() => Cache.TrimToSize(maxBytes));

        // Путь из командной строки: папка или файл
        var startPath = e.Args.Length > 0 ? e.Args[0] : null;
        var window = new MainWindow(startPath);
        window.Show();
    }

    // Светлая или тёмная тема (встроенная тема Fluent)
    public static void ApplyTheme()
    {
#pragma warning disable WPF0001 // ThemeMode в WPF пока помечен как экспериментальный
        Current.ThemeMode = Settings.Theme == AppTheme.Dark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Запоминаем последнюю папку и прочие настройки
        Settings.Save();
        base.OnExit(e);
    }
}
