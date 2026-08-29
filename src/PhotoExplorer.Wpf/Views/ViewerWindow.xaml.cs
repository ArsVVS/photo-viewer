using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoExplorer.Core;
using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Wpf.Views;

// Полноэкранный просмотр изображений
public partial class ViewerWindow : Window
{
    private readonly List<ImageFileInfo> _files;
    private int _index;

    // Загруженные и загружающиеся картинки: текущая и соседние
    private readonly Dictionary<string, Task<BitmapSource?>> _images = [];

    // Текущая картинка
    private BitmapSource? _image;

    // Масштаб и сдвиг картинки на экране
    private double _scale = 1;
    private double _offsetX;
    private double _offsetY;

    /// <summary>Номер последнего просмотренного изображения.</summary>
    public int CurrentIndex => _index;

    public ViewerWindow(List<ImageFileInfo> files, int index)
    {
        InitializeComponent();
        _files = files;
        _index = index;
        Loaded += (_, _) => _ = ShowCurrentAsync();
    }

    // Показывает изображение с номером _index
    private async Task ShowCurrentAsync()
    {
        var file = _files[_index];
        Title = file.Name;

        var image = await GetImageAsync(file.FullPath);
        PreloadNeighbors();

        // Пока картинка грузилась, пользователь мог перелистнуть дальше
        if (file != _files[_index])
            return;

        _image = image;
        PhotoImage.Source = image;
        ErrorText.Visibility = image == null ? Visibility.Visible : Visibility.Collapsed;
        FitToScreen();
    }

    // Берёт картинку из уже загруженных или запускает загрузку
    private Task<BitmapSource?> GetImageAsync(string path)
    {
        if (!_images.TryGetValue(path, out var task))
        {
            task = Task.Run(() =>
            {
                using var bitmap = ThumbnailGenerator.LoadBitmap(path);
                return bitmap != null ? ImageHelper.FromSkBitmap(bitmap) : null;
            });
            _images[path] = task;
        }
        return task;
    }

    // Заранее загружает соседние изображения, остальные выкидывает из памяти
    private void PreloadNeighbors()
    {
        var keep = new HashSet<string>();
        for (int i = _index - 1; i <= _index + 1; i++)
        {
            if (i >= 0 && i < _files.Count)
                keep.Add(_files[i].FullPath);
        }

        foreach (var path in _images.Keys.ToList())
        {
            if (!keep.Contains(path))
                _images.Remove(path);
        }
        foreach (var path in keep)
            GetImageAsync(path);
    }

    private void Next()
    {
        if (_index < _files.Count - 1)
        {
            _index++;
            _ = ShowCurrentAsync();
        }
    }

    private void Previous()
    {
        if (_index > 0)
        {
            _index--;
            _ = ShowCurrentAsync();
        }
    }

    // Размер картинки в единицах WPF при масштабе 100% (с учётом масштаба экрана Windows)
    private Size ImageSize()
    {
        if (_image == null)
            return new Size(0, 0);
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Size(_image.PixelWidth / dpi.DpiScaleX, _image.PixelHeight / dpi.DpiScaleY);
    }

    // Вписывает картинку в экран (маленькие картинки не растягиваются)
    private void FitToScreen()
    {
        var size = ImageSize();
        if (size.Width == 0)
            return;
        _scale = Math.Min(1, Math.Min(ViewArea.ActualWidth / size.Width, ViewArea.ActualHeight / size.Height));
        CenterImage();
    }

    private void CenterImage()
    {
        var size = ImageSize();
        _offsetX = (ViewArea.ActualWidth - size.Width * _scale) / 2;
        _offsetY = (ViewArea.ActualHeight - size.Height * _scale) / 2;
        ApplyTransform();
    }

    // Применяет масштаб и сдвиг к картинке на экране
    private void ApplyTransform()
    {
        var size = ImageSize();
        PhotoImage.Width = size.Width * _scale;
        PhotoImage.Height = size.Height * _scale;
        Canvas.SetLeft(PhotoImage, _offsetX);
        Canvas.SetTop(PhotoImage, _offsetY);
    }

    private void ViewArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        FitToScreen();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right:
                Next();
                break;
            case Key.Left:
                Previous();
                break;
            case Key.Escape:
                Close();
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
