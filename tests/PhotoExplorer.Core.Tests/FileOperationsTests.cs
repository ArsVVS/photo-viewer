namespace PhotoExplorer.Core.Tests;

public class FileOperationsTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData("bad:name.jpg")]
    [InlineData("name.jpg.")]
    public void Rename_InvalidName_Throws(string newName)
    {
        var path = _folder.CreateImage("photo.jpg");

        Assert.Throws<ArgumentException>(() => FileOperations.Rename(path, newName));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Rename_ToExistingName_Throws()
    {
        var path = _folder.CreateImage("photo.jpg");
        var other = _folder.CreateImage("other.jpg");

        Assert.Throws<IOException>(() => FileOperations.Rename(path, "other.jpg"));
        Assert.True(File.Exists(path));
        Assert.True(File.Exists(other));
    }
}
