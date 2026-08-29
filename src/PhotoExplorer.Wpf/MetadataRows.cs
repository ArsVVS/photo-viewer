using System.IO;
using PhotoExplorer.Core;
using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Wpf;

// Строка таблицы «название – значение»
public record InfoRow(string Name, string Value);

// Превращает сведения о файле в строки для показа пользователю
public static class MetadataRows
{
    public static List<InfoRow> Build(ImageFileInfo file, ImageMetadata meta)
    {
        var rows = new List<InfoRow>
        {
            new("Имя", file.Name),
            new("Папка", Path.GetDirectoryName(file.FullPath) ?? ""),
            new("Размер файла", Formatting.FormatSize(file.Size)),
            new("Изменён", file.LastWriteTime.ToString("dd.MM.yyyy HH:mm:ss")),
            new("Разрешение", $"{meta.Width} x {meta.Height}"),
            new("Формат", meta.Format)
        };

        // Необязательные поля EXIF добавляем, только если они есть
        AddIfSet(rows, "Дата съёмки", meta.DateTaken?.ToString("dd.MM.yyyy HH:mm:ss"));
        AddIfSet(rows, "Камера", meta.Camera);
        AddIfSet(rows, "Выдержка", meta.ExposureTime);
        AddIfSet(rows, "Диафрагма", meta.FNumber.HasValue ? $"f/{meta.FNumber}" : null);
        AddIfSet(rows, "ISO", meta.Iso?.ToString());
        AddIfSet(rows, "Ориентация", meta.Orientation?.ToString());
        return rows;
    }

    private static void AddIfSet(List<InfoRow> rows, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            rows.Add(new InfoRow(name, value));
    }
}
