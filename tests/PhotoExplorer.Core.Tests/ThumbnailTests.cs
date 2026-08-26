using SkiaSharp;

namespace PhotoExplorer.Core.Tests;

public class ThumbnailTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Generate_KeepsAspectRatio()
    {
        var path = _folder.CreateImage("wide.png", 800, 400, SKEncodedImageFormat.Png);

        var thumb = ThumbnailGenerator.Generate(path, 160);

        Assert.NotNull(thumb);
        Assert.Equal(800, thumb.OriginalWidth);
        Assert.Equal(400, thumb.OriginalHeight);
        using var bitmap = SKBitmap.Decode(thumb.Data);
        Assert.Equal(160, bitmap.Width);
        Assert.Equal(80, bitmap.Height);
    }

    [Fact]
    public void Generate_CorruptedFile_ReturnsNull()
    {
        var path = _folder.CreateFile("broken.jpg", [0xFF, 0xD8, 0x00, 0x01, 0x02, 0x03]);

        Assert.Null(ThumbnailGenerator.Generate(path, 160));
    }

    [Fact]
    public async Task Cache_SecondRequest_TakenFromCache()
    {
        var path = _folder.CreateImage("photo.jpg", 400, 300);
        var cache = new ThumbnailCache(Path.Combine(_folder.Path, "thumbs.db"));

        var first = await cache.GetOrCreateAsync(path, 100);
        var second = await cache.GetOrCreateAsync(path, 100);

        Assert.NotNull(first);
        Assert.Equal(first.Data, second!.Data);
        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);
    }

    [Fact]
    public async Task Cache_ChangedFile_IsRebuilt()
    {
        var path = _folder.CreateImage("photo.jpg", 400, 300);
        var cache = new ThumbnailCache(Path.Combine(_folder.Path, "thumbs.db"));
        await cache.GetOrCreateAsync(path, 100);

        // Перезаписываем файл картинкой другого размера
        _folder.CreateImage("photo.jpg", 200, 400);
        File.SetLastWriteTime(path, DateTime.Now.AddMinutes(1));
        var thumb = await cache.GetOrCreateAsync(path, 100);

        Assert.Equal(0, cache.Hits);
        Assert.Equal(200, thumb!.OriginalWidth);
        Assert.Equal(400, thumb.OriginalHeight);
    }

    [Fact]
    public async Task Cache_RemoveMissing_DeletesRecordsOfDeletedFiles()
    {
        var keep = _folder.CreateImage("keep.jpg");
        var remove = _folder.CreateImage("remove.jpg");
        var cache = new ThumbnailCache(Path.Combine(_folder.Path, "thumbs.db"));
        await cache.GetOrCreateAsync(keep, 100);
        await cache.GetOrCreateAsync(remove, 100);

        File.Delete(remove);
        int removed = cache.RemoveMissing();

        Assert.Equal(1, removed);
        Assert.Equal(1, cache.GetStats().Count);
    }

    [Fact]
    public async Task Cache_TrimToSize_RemovesOldestRecords()
    {
        var cache = new ThumbnailCache(Path.Combine(_folder.Path, "thumbs.db"));
        var paths = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            paths.Add(_folder.CreateImage($"img{i}.png", 300, 300, SKEncodedImageFormat.Png));
            await cache.GetOrCreateAsync(paths[i], 200);
        }
        // Первую картинку открываем ещё раз – она становится самой «свежей»
        await cache.GetOrCreateAsync(paths[0], 200);
        long oneThumb = ThumbnailGenerator.Generate(paths[0], 200)!.Data.Length;

        cache.TrimToSize(oneThumb * 2 + 100);

        Assert.Equal(2, cache.GetStats().Count);
        int hitsBefore = cache.Hits;
        await cache.GetOrCreateAsync(paths[0], 200);
        Assert.Equal(hitsBefore + 1, cache.Hits);
    }
}
