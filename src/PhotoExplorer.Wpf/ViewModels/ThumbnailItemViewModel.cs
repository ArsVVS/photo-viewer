using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Wpf.ViewModels;

// Одна плитка в сетке миниатюр
public partial class ThumbnailItemViewModel(ImageFileInfo file) : ObservableObject
{
    public ImageFileInfo File { get; } = file;
    public string Name => File.Name;
    public string Type => File.Extension.TrimStart('.').ToUpperInvariant();

    // Подпись под именем: разрешение и тип
    public string Caption => File.Width.HasValue ? $"{File.Width} x {File.Height}  {Type}" : Type;

    [ObservableProperty]
    public partial ImageSource? Thumbnail { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    // Миниатюра загружена (thumbnail = null – файл повреждён)
    public void SetThumbnail(ImageSource? thumbnail)
    {
        Thumbnail = thumbnail;
        HasError = thumbnail == null;
        IsLoading = false;
        OnPropertyChanged(nameof(Caption));
    }
}
