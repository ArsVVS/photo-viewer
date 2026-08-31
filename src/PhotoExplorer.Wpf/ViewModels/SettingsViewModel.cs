using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoExplorer.Core;

namespace PhotoExplorer.Wpf.ViewModels;

// Окно настроек работает с копией значений, в App.Settings они попадают только по кнопке OK
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial AppTheme Theme { get; set; }

    [ObservableProperty]
    public partial int ThumbnailSize { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartFromSelectedFolder))]
    public partial bool StartFromLastFolder { get; set; }

    // Для второго переключателя
    public bool StartFromSelectedFolder
    {
        get => !StartFromLastFolder;
        set => StartFromLastFolder = !value;
    }

    [ObservableProperty]
    public partial string StartFolder { get; set; }

    [ObservableProperty]
    public partial bool ShowHiddenFolders { get; set; }

    // Расширения через запятую: ".jpg, .png"
    [ObservableProperty]
    public partial string Extensions { get; set; }

    [ObservableProperty]
    public partial int SlideShowInterval { get; set; }

    [ObservableProperty]
    public partial bool SlideShowLoop { get; set; }

    [ObservableProperty]
    public partial int MaxCacheSizeMb { get; set; }

    public ObservableCollection<string> Favorites { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveFavoriteCommand))]
    public partial string? SelectedFavorite { get; set; }

    // Статистика кеша
    [ObservableProperty]
    public partial string CacheInfo { get; set; } = "";

    public SettingsViewModel(AppSettings settings)
    {
        Theme = settings.Theme;
        ThumbnailSize = settings.ThumbnailSize;
        StartFromLastFolder = settings.StartFolderMode == StartFolderMode.LastOpened;
        StartFolder = settings.StartFolder ?? "";
        ShowHiddenFolders = settings.ShowHiddenFolders;
        Extensions = string.Join(", ", settings.Extensions);
        SlideShowInterval = settings.SlideShowInterval;
        SlideShowLoop = settings.SlideShowLoop;
        MaxCacheSizeMb = settings.MaxCacheSizeMb;
        Favorites = new ObservableCollection<string>(settings.Favorites);
        UpdateCacheInfo();
    }

    // Проверяет введённые значения. Возвращает текст ошибки или null
    public string? Validate()
    {
        if (ParseExtensions().Count == 0)
            return "Укажите хотя бы одно расширение файлов.";
        if (SlideShowInterval < 1 || SlideShowInterval > 3600)
            return "Интервал слайд-шоу должен быть от 1 до 3600 секунд.";
        if (MaxCacheSizeMb < 10)
            return "Размер кеша должен быть не меньше 10 МБ.";
        if (!StartFromLastFolder && !System.IO.Directory.Exists(StartFolder))
            return "Стартовая папка не найдена.";
        return null;
    }

    // Записывает значения в настройки приложения
    public void ApplyTo(AppSettings settings)
    {
        settings.Theme = Theme;
        settings.ThumbnailSize = ThumbnailSize;
        settings.StartFolderMode = StartFromLastFolder ? StartFolderMode.LastOpened : StartFolderMode.Selected;
        settings.StartFolder = StartFolder;
        settings.ShowHiddenFolders = ShowHiddenFolders;
        settings.Extensions = ParseExtensions();
        settings.SlideShowInterval = SlideShowInterval;
        settings.SlideShowLoop = SlideShowLoop;
        settings.MaxCacheSizeMb = MaxCacheSizeMb;
        settings.Favorites = Favorites.ToList();
    }

    // "jpg, .PNG" -> [".jpg", ".png"]
    private List<string> ParseExtensions()
    {
        return Extensions.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLowerInvariant())
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .Distinct()
            .ToList();
    }

    private bool CanRemoveFavorite() => SelectedFavorite != null;

    [RelayCommand(CanExecute = nameof(CanRemoveFavorite))]
    private void RemoveFavorite()
    {
        if (SelectedFavorite != null)
            Favorites.Remove(SelectedFavorite);
    }

    [RelayCommand]
    private void CleanupCache()
    {
        int removed = App.Cache.RemoveMissing();
        UpdateCacheInfo();
        CacheInfo += $"\nУдалено устаревших записей: {removed}";
    }

    [RelayCommand]
    private void ClearCache()
    {
        App.Cache.Clear();
        UpdateCacheInfo();
    }

    private void UpdateCacheInfo()
    {
        var stats = App.Cache.GetStats();
        CacheInfo = $"Записей: {stats.Count}\n" +
                    $"Размер базы: {Formatting.FormatSize(stats.DatabaseBytes)}\n" +
                    $"Попаданий в кеш за сессию: {stats.HitRate:0}% ({stats.Hits} из {stats.Hits + stats.Misses})";
    }
}
