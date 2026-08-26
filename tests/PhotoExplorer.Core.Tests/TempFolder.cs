using SkiaSharp;

namespace PhotoExplorer.Core.Tests;

// Временная папка для теста, удаляется после него
public sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PhotoExplorerTests", Guid.NewGuid().ToString("N"));

    public TempFolder()
    {
        Directory.CreateDirectory(Path);
    }

    // Создаёт картинку заданного размера и формата, возвращает полный путь
    public string CreateImage(string relativePath, int width = 64, int height = 48, SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);

        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SkyBlue);
            using var paint = new SKPaint { Color = SKColors.OrangeRed };
            canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
        }
        using var data = bitmap.Encode(format, 90);
        File.WriteAllBytes(fullPath, data.ToArray());
        return fullPath;
    }

    // Создаёт файл с произвольным содержимым
    public string CreateFile(string relativePath, byte[] content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        // SQLite может ещё держать файл базы в пуле соединений
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Path, true);
        }
        catch (IOException)
        {
            // Не удалось удалить – не страшно, это временная папка
        }
    }
}
