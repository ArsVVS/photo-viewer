using Microsoft.VisualBasic.FileIO;

namespace PhotoExplorer.Core;

/// <summary>Переименование и удаление файлов.</summary>
public static class FileOperations
{
    /// <summary>Проверяет, можно ли использовать строку как имя файла.</summary>
    public static bool IsValidFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;
        // Windows не любит точку или пробел в конце имени
        if (name.EndsWith('.') || name.EndsWith(' '))
            return false;
        return name != "." && name != "..";
    }

    /// <summary>Переименовывает файл и возвращает новый путь.</summary>
    public static string Rename(string path, string newName)
    {
        if (!IsValidFileName(newName))
            throw new ArgumentException($"Недопустимое имя файла: «{newName}»", nameof(newName));
        if (!File.Exists(path))
            throw new FileNotFoundException("Файл не найден", path);

        var newPath = Path.Combine(Path.GetDirectoryName(path)!, newName);

        // Имя не поменялось – ничего не делаем
        if (string.Equals(path, newPath, StringComparison.Ordinal))
            return path;

        // Смена только регистра букв – это тот же файл, такое разрешаем
        bool sameFile = string.Equals(path, newPath, StringComparison.OrdinalIgnoreCase);
        if (!sameFile && (File.Exists(newPath) || Directory.Exists(newPath)))
            throw new IOException($"Файл «{newName}» уже существует");

        File.Move(path, newPath);
        return newPath;
    }

    /// <summary>Удаляет файл в корзину.</summary>
    public static void DeleteToRecycleBin(string path)
    {
        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }
}
