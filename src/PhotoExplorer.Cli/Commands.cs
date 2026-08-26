using System.Text.Encodings.Web;
using System.Text.Json;
using PhotoExplorer.Core;
using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Cli;

// Обработчики команд CLI. Возвращают код выхода.
public static class Commands
{
    public const int Ok = 0;
    public const int ArgumentError = 1;
    public const int FileError = 2;

    private static readonly AppSettings Settings = AppSettings.Load();

    public static int List(string folder, string sort, bool descending, string format)
    {
        if (!Directory.Exists(folder))
            return Error($"Папка не найдена: {folder}", FileError);

        var files = new FolderBrowser(Settings).GetImages(folder);
        var field = Enum.Parse<SortField>(sort, ignoreCase: true);
        files = ImageSorter.Sort(files, field, descending);

        if (format == "json")
            PrintJson(files);
        else
            PrintTable(files);
        return Ok;
    }

    public static async Task<int> SearchAsync(string folder, string? name, bool recursive, string? types,
        DateTime? from, DateTime? to, CancellationToken token)
    {
        if (!Directory.Exists(folder))
            return Error($"Папка не найдена: {folder}", FileError);
        if (from > to)
            return Error("Дата --from позже даты --to", ArgumentError);

        var options = new SearchOptions
        {
            Name = name,
            Recursive = recursive,
            DateFrom = from,
            DateTo = to,
            Extensions = types?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        };

        // Выводим найденное сразу, не дожидаясь конца поиска
        int count = 0;
        var search = new ImageSearch(new FolderBrowser(Settings));
        await foreach (var file in search.SearchAsync(folder, options, token))
        {
            Console.WriteLine($"{file.FullPath}  ({Formatting.FormatSize(file.Size)}, {file.LastWriteTime:yyyy-MM-dd HH:mm})");
            count++;
        }
        Console.WriteLine($"Найдено: {count}");
        return Ok;
    }

    public static int Info(string path)
    {
        if (!File.Exists(path))
            return Error($"Файл не найден: {path}", FileError);

        var file = ImageFileInfo.FromFile(path);
        var meta = MetadataReader.Read(path);

        Console.WriteLine($"Файл:          {file.FullPath}");
        Console.WriteLine($"Размер файла:  {Formatting.FormatSize(file.Size)}");
        Console.WriteLine($"Изменён:       {file.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Разрешение:    {meta.Width} x {meta.Height}");
        Console.WriteLine($"Формат:        {meta.Format}");
        PrintIfSet("Дата съёмки:", meta.DateTaken?.ToString("yyyy-MM-dd HH:mm:ss"));
        PrintIfSet("Камера:", meta.Camera);
        PrintIfSet("Выдержка:", meta.ExposureTime);
        PrintIfSet("Диафрагма:", meta.FNumber.HasValue ? $"f/{meta.FNumber}" : null);
        PrintIfSet("ISO:", meta.Iso?.ToString());
        PrintIfSet("Ориентация:", meta.Orientation?.ToString());
        return Ok;
    }

    public static async Task<int> ThumbsBuildAsync(string? cachePath, string folder, bool recursive, int size)
    {
        if (!Directory.Exists(folder))
            return Error($"Папка не найдена: {folder}", FileError);
        if (size < 16 || size > 1024)
            return Error("Размер миниатюры должен быть от 16 до 1024", ArgumentError);

        // Собираем все файлы (поиск без условий)
        var files = new List<ImageFileInfo>();
        var search = new ImageSearch(new FolderBrowser(Settings));
        await foreach (var file in search.SearchAsync(folder, new SearchOptions { Recursive = recursive }))
            files.Add(file);

        var cache = new ThumbnailCache(cachePath);
        var loader = new ThumbnailLoader(cache);
        int errors = 0;
        loader.ThumbnailReady += (path, data) =>
        {
            if (data == null)
                Interlocked.Increment(ref errors);
        };
        loader.ProgressChanged += (done, total) =>
        {
            lock (loader)
                Console.Write($"\rМиниатюры: {done}/{total}");
        };

        var started = DateTime.Now;
        await loader.LoadAsync(files, size);
        Console.WriteLine();

        var stats = cache.GetStats();
        Console.WriteLine($"Готово за {(DateTime.Now - started).TotalSeconds:0.0} с. Из кеша: {stats.Hits}, построено: {stats.Misses - errors}, ошибок: {errors}");
        return Ok;
    }

    public static async Task<int> ThumbsExportAsync(string? cachePath, string file, string output, int size)
    {
        if (!File.Exists(file))
            return Error($"Файл не найден: {file}", FileError);
        if (size < 16 || size > 4096)
            return Error("Размер миниатюры должен быть от 16 до 4096", ArgumentError);

        var thumb = await new ThumbnailCache(cachePath).GetOrCreateAsync(file, size);
        if (thumb == null)
            return Error($"Не удалось прочитать изображение: {file}", FileError);

        try
        {
            File.WriteAllBytes(output, thumb.Data);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Error($"Не удалось сохранить файл: {e.Message}", FileError);
        }
        Console.WriteLine($"Миниатюра сохранена: {Path.GetFullPath(output)}");
        return Ok;
    }

    public static int CacheStats(string? cachePath)
    {
        var cache = new ThumbnailCache(cachePath);
        var stats = cache.GetStats();
        Console.WriteLine($"База:          {cache.DatabasePath}");
        Console.WriteLine($"Записей:       {stats.Count}");
        Console.WriteLine($"Размер базы:   {Formatting.FormatSize(stats.DatabaseBytes)}");
        return Ok;
    }

    public static int CacheCleanup(string? cachePath)
    {
        int removed = new ThumbnailCache(cachePath).RemoveMissing();
        Console.WriteLine($"Удалено устаревших записей: {removed}");
        return Ok;
    }

    public static int CacheClear(string? cachePath)
    {
        new ThumbnailCache(cachePath).Clear();
        Console.WriteLine("Кеш очищен");
        return Ok;
    }

    private static void PrintIfSet(string title, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            Console.WriteLine($"{title,-15}{value}");
    }

    private static void PrintTable(List<ImageFileInfo> files)
    {
        int nameWidth = Math.Max(3, files.Count == 0 ? 0 : files.Max(f => f.Name.Length));
        Console.WriteLine($"{"Имя".PadRight(nameWidth)}  {"Размер",10}  {"Изменён",-16}  Тип");
        Console.WriteLine(new string('-', nameWidth + 40));
        foreach (var f in files)
            Console.WriteLine($"{f.Name.PadRight(nameWidth)}  {Formatting.FormatSize(f.Size),10}  {f.LastWriteTime,-16:yyyy-MM-dd HH:mm}  {f.Extension.TrimStart('.')}");
        Console.WriteLine($"Всего: {files.Count}, {Formatting.FormatSize(files.Sum(f => f.Size))}");
    }

    private static void PrintJson(List<ImageFileInfo> files)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Чтобы русские буквы выводились как есть, а не \uXXXX
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var items = files.Select(f => new { f.Name, f.FullPath, f.Extension, f.Size, f.LastWriteTime });
        Console.WriteLine(JsonSerializer.Serialize(items, options));
    }

    private static int Error(string message, int code)
    {
        Console.Error.WriteLine(message);
        return code;
    }
}
