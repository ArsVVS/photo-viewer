using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace PhotoExplorer.Wpf;

// Превращение картинок из Core в картинки WPF
public static class ImageHelper
{
    // JPEG-байты миниатюры -> картинка WPF
    public static BitmapSource FromBytes(byte[] data)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(data);
        image.EndInit();
        // Freeze, чтобы картинку можно было передать из фонового потока в интерфейс
        image.Freeze();
        return image;
    }

    // SKBitmap -> картинка WPF (копируем пиксели как есть)
    public static BitmapSource FromSkBitmap(SKBitmap bitmap)
    {
        var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Pbgra32, null,
            bitmap.GetPixels(), bitmap.ByteCount, bitmap.RowBytes);
        source.Freeze();
        return source;
    }
}
