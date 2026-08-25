using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Core;

/// <summary>Условия поиска. Пустые поля не учитываются.</summary>
public class SearchOptions
{
    /// <summary>Подстрока или маска с * и ?.</summary>
    public string? Name { get; set; }

    /// <summary>Расширения, например ".jpg", ".png".</summary>
    public List<string>? Extensions { get; set; }

    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }

    /// <summary>Дата изменения от (включительно, по дню).</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Дата изменения до (включительно, по дню).</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Искать во вложенных папках.</summary>
    public bool Recursive { get; set; }
}

/// <summary>Поиск изображений по имени и фильтрам.</summary>
public class ImageSearch(FolderBrowser browser)
{
    /// <summary>Ищет изображения. Результаты отдаются по мере нахождения (по папкам).</summary>
    public async IAsyncEnumerable<ImageFileInfo> SearchAsync(string folder, SearchOptions options,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        // Обходим папки через стек, чтобы не писать рекурсию
        var folders = new Stack<string>();
        folders.Push(folder);

        while (folders.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var current = folders.Pop();

            // Чтение папки может быть долгим, поэтому в фоне
            var files = await Task.Run(() => browser.GetImages(current), token);
            foreach (var file in files)
            {
                if (Matches(file, options))
                    yield return file;
            }

            if (options.Recursive)
            {
                var subfolders = await Task.Run(() => browser.GetSubfolders(current), token);
                // В обратном порядке, чтобы папки обходились по алфавиту
                for (int i = subfolders.Count - 1; i >= 0; i--)
                    folders.Push(subfolders[i].Path);
            }
        }
    }

    /// <summary>Подходит ли файл под условия поиска.</summary>
    public static bool Matches(ImageFileInfo file, SearchOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Name) && !MatchesName(file.Name, options.Name))
            return false;
        if (options.Extensions is { Count: > 0 } &&
            !options.Extensions.Any(e => string.Equals(NormalizeExtension(e), file.Extension, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (options.MinSize.HasValue && file.Size < options.MinSize.Value)
            return false;
        if (options.MaxSize.HasValue && file.Size > options.MaxSize.Value)
            return false;
        if (options.DateFrom.HasValue && file.LastWriteTime.Date < options.DateFrom.Value.Date)
            return false;
        if (options.DateTo.HasValue && file.LastWriteTime.Date > options.DateTo.Value.Date)
            return false;
        return true;
    }

    /// <summary>Сравнивает имя с образцом: маска (* и ?) или подстрока, без учёта регистра.</summary>
    public static bool MatchesName(string name, string pattern)
    {
        pattern = pattern.Trim();
        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            // Превращаем маску в регулярное выражение: * – любые символы, ? – один символ
            var regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            return Regex.IsMatch(name, regex, RegexOptions.IgnoreCase);
        }
        return name.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    // "jpg" и ".JPG" превращаем в ".jpg"
    private static string NormalizeExtension(string ext)
    {
        ext = ext.Trim().ToLowerInvariant();
        return ext.StartsWith('.') ? ext : "." + ext;
    }
}
