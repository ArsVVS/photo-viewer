namespace PhotoExplorer.Core.Models;

/// <summary>Информация о файле изображения.</summary>
public class ImageFileInfo
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public long Size { get; init; }
    public DateTime LastWriteTime { get; init; }

    // Размеры заполняются, когда строится миниатюра
    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>Создаёт описание по пути к файлу.</summary>
    public static ImageFileInfo FromFile(string path)
    {
        var info = new FileInfo(path);
        return FromFileInfo(info);
    }

    /// <summary>Создаёт описание из FileInfo.</summary>
    public static ImageFileInfo FromFileInfo(FileInfo info)
    {
        return new ImageFileInfo
        {
            FullPath = info.FullName,
            Name = info.Name,
            Extension = info.Extension.ToLowerInvariant(),
            Size = info.Length,
            LastWriteTime = info.LastWriteTime
        };
    }
}
