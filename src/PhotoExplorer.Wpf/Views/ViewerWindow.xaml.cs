using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

    // Текущая картинка: исходная и повёрнутая для просмотра
    private BitmapSource? _original;
    private BitmapSource? _image;
    private int _rotation;

    // Масштаб и сдвиг картинки на экране
    private double _scale = 1;
    private double _offsetX;
    private double _offsetY;

    // Картинка вписана в экран (тогда при изменении размера окна вписываем заново)
    private bool _fitMode = true;

    // Перетаскивание мышью
    private Point? _dragStart;
    private double _dragOffsetX;
    private double _dragOffsetY;

    // Таймер слайд-шоу
    private readonly DispatcherTimer _slideShowTimer = new();

    private const double MinScale = 0.02;
    private const double MaxScale = 20;

    /// <summary>Номер последнего просмотренного изображения.</summary>
    public int CurrentIndex => _index;

    public ViewerWindow(List<ImageFileInfo> files, int index)
    {
        InitializeComponent();
        _files = files;
        _index = index;
        _slideShowTimer.Tick += SlideShowTimer_Tick;
        Loaded += (_, _) =>
        {
            // При запуске с файлом окно открывается сразу – забираем фокус клавиатуры себе
            Activate();
            Focus();
            _ = ShowCurrentAsync();
        };
        Closed += (_, _) => _slideShowTimer.Stop();
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

        _original = image;
        _rotation = 0;
        if (InfoPanel.Visibility == Visibility.Visible)
            _ = UpdateInfoAsync();
        ErrorText.Visibility = image == null ? Visibility.Visible : Visibility.Collapsed;
        UpdateRotatedImage();
    }

    // I – показать/скрыть информацию о файле
    private void ToggleInfo()
    {
        if (InfoPanel.Visibility == Visibility.Visible)
        {
            InfoPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            InfoPanel.Visibility = Visibility.Visible;
            _ = UpdateInfoAsync();
        }
    }

    private async Task UpdateInfoAsync()
    {
        var file = _files[_index];
        InfoPosition.Text = $"{_index + 1} из {_files.Count}";
        var meta = await Task.Run(() => MetadataReader.Read(file.FullPath));
        if (file == _files[_index])
            InfoList.ItemsSource = MetadataRows.Build(file, meta);
    }

    // Поворот только для просмотра, сам файл не меняется
    private void Rotate(int degrees)
    {
        if (_original == null)
            return;
        _rotation = (_rotation + degrees + 360) % 360;
        UpdateRotatedImage();
    }

    private void UpdateRotatedImage()
    {
        _image = _original;
        if (_original != null && _rotation != 0)
        {
            var rotated = new TransformedBitmap(_original, new RotateTransform(_rotation));
            rotated.Freeze();
            _image = rotated;
        }
        PhotoImage.Source = _image;
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

    // Space – запустить слайд-шоу, повторное нажатие – пауза
    private void ToggleSlideShow()
    {
        if (_slideShowTimer.IsEnabled)
        {
            _slideShowTimer.Stop();
            SlideShowText.Text = "Пауза";
        }
        else
        {
            // Интервал берём из настроек при каждом запуске
            _slideShowTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, App.Settings.SlideShowInterval));
            _slideShowTimer.Start();
            SlideShowText.Text = $"Слайд-шоу ({App.Settings.SlideShowInterval} с)";
        }
        SlideShowBadge.Visibility = Visibility.Visible;
    }

    private void SlideShowTimer_Tick(object? sender, EventArgs e)
    {
        // Подсказку показываем только в начале, чтобы не мешала смотреть
        SlideShowBadge.Visibility = Visibility.Collapsed;

        if (_index < _files.Count - 1)
        {
            Next();
        }
        else if (App.Settings.SlideShowLoop)
        {
            // Дошли до конца – начинаем сначала
            _index = 0;
            _ = ShowCurrentAsync();
        }
        else
        {
            _slideShowTimer.Stop();
            SlideShowText.Text = "Слайд-шоу закончилось";
            SlideShowBadge.Visibility = Visibility.Visible;
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
        _fitMode = true;
        _scale = Math.Min(1, Math.Min(ViewArea.ActualWidth / size.Width, ViewArea.ActualHeight / size.Height));
        CenterImage();
    }

    // Масштаб 100% – один пиксель картинки на один пиксель экрана
    private void ActualSize()
    {
        _fitMode = false;
        _scale = 1;
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
        if (_fitMode)
            FitToScreen();
        else
            CenterImage();
    }

    // Колесо мыши – масштаб относительно курсора
    private void ViewArea_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_image == null)
            return;

        double factor = e.Delta > 0 ? 1.2 : 1 / 1.2;
        double newScale = Math.Clamp(_scale * factor, MinScale, MaxScale);

        // Точка под курсором должна остаться на месте
        var mouse = e.GetPosition(ViewArea);
        _offsetX = mouse.X - (mouse.X - _offsetX) * newScale / _scale;
        _offsetY = mouse.Y - (mouse.Y - _offsetY) * newScale / _scale;
        _scale = newScale;
        _fitMode = false;
        ApplyTransform();
    }

    // Перетаскивание картинки мышью
    private void ViewArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(ViewArea);
        _dragOffsetX = _offsetX;
        _dragOffsetY = _offsetY;
        ViewArea.CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    private void ViewArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart == null)
            return;
        var position = e.GetPosition(ViewArea);
        _offsetX = _dragOffsetX + position.X - _dragStart.Value.X;
        _offsetY = _dragOffsetY + position.Y - _dragStart.Value.Y;
        _fitMode = false;
        ApplyTransform();
    }

    private void ViewArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragStart = null;
        ViewArea.ReleaseMouseCapture();
        Cursor = null;
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
            case Key.D0:
            case Key.NumPad0:
                FitToScreen();
                break;
            case Key.D1:
            case Key.NumPad1:
                ActualSize();
                break;
            case Key.R:
                Rotate(90);
                break;
            case Key.L:
                Rotate(-90);
                break;
            case Key.I:
                ToggleInfo();
                break;
            case Key.Space:
                ToggleSlideShow();
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
