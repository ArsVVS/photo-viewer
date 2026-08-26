using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Core.Tests;

public class BrowseAndSearchTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly FolderBrowser _browser = new(new AppSettings());

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void GetImages_ReturnsOnlySupportedFiles()
    {
        _folder.CreateImage("a.jpg");
        _folder.CreateImage("b.png");
        _folder.CreateFile("notes.txt", [1, 2, 3]);
        _folder.CreateFile("video.mp4", [1, 2, 3]);

        var names = _browser.GetImages(_folder.Path).Select(f => f.Name).OrderBy(n => n);

        Assert.Equal(["a.jpg", "b.png"], names);
    }

    [Fact]
    public void Sort_ByName_IsNatural()
    {
        var files = new[] { "img10.jpg", "img2.jpg", "IMG1.jpg", "img2a.jpg" }.Select(n => Make(n, 0));

        var sorted = ImageSorter.Sort(files, SortField.Name).Select(f => f.Name);

        Assert.Equal(["IMG1.jpg", "img2.jpg", "img2a.jpg", "img10.jpg"], sorted);
    }

    [Fact]
    public void Sort_BySize_Descending()
    {
        var files = new[] { Make("a.jpg", 300), Make("b.jpg", 100), Make("c.jpg", 200) };

        var sorted = ImageSorter.Sort(files, SortField.Size, descending: true).Select(f => f.Name);

        Assert.Equal(["a.jpg", "c.jpg", "b.jpg"], sorted);
    }

    [Theory]
    [InlineData("*отпуск*", new[] { "Отпуск_море.jpg", "фото отпуск 2.png" })]
    [InlineData("img?.jpg", new[] { "img1.jpg" })]
    [InlineData("мор", new[] { "Отпуск_море.jpg" })]
    public async Task Search_ByNameOrMask(string pattern, string[] expected)
    {
        _folder.CreateImage("Отпуск_море.jpg");
        _folder.CreateImage("фото отпуск 2.png", format: SkiaSharp.SKEncodedImageFormat.Png);
        _folder.CreateImage("img1.jpg");
        _folder.CreateImage("img12.jpg");

        var found = await Search(new SearchOptions { Name = pattern });

        Assert.Equal(expected.OrderBy(n => n), found);
    }

    [Fact]
    public async Task Search_Recursive_FindsInSubfolders()
    {
        _folder.CreateImage("top.jpg");
        _folder.CreateImage(Path.Combine("2025", "summer", "deep.jpg"));

        var flat = await Search(new SearchOptions());
        var recursive = await Search(new SearchOptions { Recursive = true });

        Assert.Equal(["top.jpg"], flat);
        Assert.Equal(["deep.jpg", "top.jpg"], recursive);
    }

    [Fact]
    public async Task Search_FilterBySize()
    {
        _folder.CreateFile("small.jpg", new byte[100]);
        _folder.CreateFile("medium.jpg", new byte[1000]);
        _folder.CreateFile("large.jpg", new byte[10000]);

        var found = await Search(new SearchOptions { MinSize = 500, MaxSize = 5000 });

        Assert.Equal(["medium.jpg"], found);
    }

    private async Task<List<string>> Search(SearchOptions options)
    {
        var result = new List<string>();
        await foreach (var file in new ImageSearch(_browser).SearchAsync(_folder.Path, options))
            result.Add(file.Name);
        return result.OrderBy(n => n).ToList();
    }

    private static ImageFileInfo Make(string name, long size) => new()
    {
        FullPath = name,
        Name = name,
        Extension = Path.GetExtension(name),
        Size = size
    };
}
