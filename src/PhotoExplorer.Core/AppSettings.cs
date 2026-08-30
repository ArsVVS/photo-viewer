using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoExplorer.Core;

public enum AppTheme { Light, Dark }

public enum StartFolderMode { LastOpened, Selected }

/// <summary>Настройки приложения, хранятся в settings.json.</summary>
public class AppSettings
{
    public const int SmallThumbnail = 100;
    public const int MediumThumbnail = 160;
    public const int LargeThumbnail = 240;

    public AppTheme Theme { get; set; } = AppTheme.Light;
    public int ThumbnailSize { get; set; } = MediumThumbnail;
    public StartFolderMode StartFolderMode { get; set; } = StartFolderMode.LastOpened;
    public string? StartFolder { get; set; }
    public string? LastFolder { get; set; }
    public bool ShowHiddenFolders { get; set; }
    public List<string> Extensions { get; set; } = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];
    public int SlideShowInterval { get; set; } = 3;
    public bool SlideShowLoop { get; set; } = true;
    public int MaxCacheSizeMb { get; set; } = 500;
    public List<string> Favorites { get; set; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Русские буквы в путях сохраняем как есть
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Путь к файлу настроек по умолчанию.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhotoExplorer", "settings.json");

    /// <summary>Загружает настройки из файла, при ошибке возвращает настройки по умолчанию.</summary>
    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception)
        {
            // Испорченный файл – просто начинаем с настроек по умолчанию
        }
        return new AppSettings();
    }

    /// <summary>Сохраняет настройки в файл.</summary>
    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>Проверяет, поддерживается ли расширение файла.</summary>
    public bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path);
        return Extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Копия настроек (для окна настроек).</summary>
    public AppSettings Clone()
    {
        var json = JsonSerializer.Serialize(this, JsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)!;
    }
}
