using SkiaSharp;

namespace PhotoExplorer.Core;

/// <summary>Готовая миниатюра: JPEG-байты и размеры оригинала.</summary>
public record ThumbnailResult(byte[] Data, int OriginalWidth, int OriginalHeight);

/// <summary>Построение миниатюр и загрузка изображений через SkiaSharp.</summary>
public static class ThumbnailGenerator
{
    /// <summary>Строит JPEG-миниатюру с длинной стороной size. Для повреждённого файла возвращает null.</summary>
    public static ThumbnailResult? Generate(string path, int size, int quality = 85)
    {
        var loaded = Load(path, size);
        if (loaded == null)
            return null;

        using var bitmap = loaded.Value.Bitmap;

        // У JPEG нет прозрачности, поэтому рисуем картинку на белом фоне
        using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawBitmap(bitmap, 0, 0);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        return new ThumbnailResult(data.ToArray(), loaded.Value.Width, loaded.Value.Height);
    }

    /// <summary>Загружает изображение с учётом EXIF-ориентации. maxSize = 0 – без уменьшения.</summary>
    public static SKBitmap? LoadBitmap(string path, int maxSize = 0)
    {
        return Load(path, maxSize)?.Bitmap;
    }

    // Загрузка: уменьшенное декодирование, точное уменьшение и поворот по EXIF.
    // Width и Height – размеры оригинала уже после поворота.
    private static (SKBitmap Bitmap, int Width, int Height)? Load(string path, int maxSize)
    {
        try
        {
            using var codec = SKCodec.Create(path);
            if (codec == null)
                return null;

            int width = codec.Info.Width;
            int height = codec.Info.Height;

            // Какого размера картинка нам нужна
            int targetW = width, targetH = height;
            if (maxSize > 0 && Math.Max(width, height) > maxSize)
            {
                double scale = (double)maxSize / Math.Max(width, height);
                targetW = Math.Max(1, (int)Math.Round(width * scale));
                targetH = Math.Max(1, (int)Math.Round(height * scale));
            }

            // JPEG умеет сразу декодироваться в 1/2, 1/4, 1/8 размера – так быстрее и меньше памяти.
            // Для остальных форматов тут вернётся исходный размер.
            var decodeSize = codec.GetScaledDimensions((float)targetW / width);
            if (decodeSize.Width < targetW || decodeSize.Height < targetH)
                decodeSize = new SKSizeI(width, height);

            var decodeInfo = new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var decoded = new SKBitmap(decodeInfo);
            var status = codec.GetPixels(decodeInfo, decoded.GetPixels());
            // IncompleteInput – файл обрезан, но часть картинки есть; остальное считаем ошибкой
            if (status != SKCodecResult.Success && status != SKCodecResult.IncompleteInput)
            {
                decoded.Dispose();
                return null;
            }

            // Доуменьшаем до точного размера
            if (decoded.Width != targetW || decoded.Height != targetH)
            {
                var resized = decoded.Resize(new SKImageInfo(targetW, targetH, SKColorType.Bgra8888, SKAlphaType.Premul),
                    new SKSamplingOptions(SKCubicResampler.Mitchell));
                decoded.Dispose();
                if (resized == null)
                    return null;
                decoded = resized;
            }

            var origin = codec.EncodedOrigin;
            var oriented = ApplyOrientation(decoded, origin);
            if (oriented != decoded)
                decoded.Dispose();

            // Для повёрнутых на 90° фото ширина и высота меняются местами
            if (IsRotated90(origin))
                (width, height) = (height, width);

            return (oriented, width, height);
        }
        catch (Exception)
        {
            // Повреждённый или неподдерживаемый файл
            return null;
        }
    }

    private static bool IsRotated90(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    // Поворачивает/отражает картинку так, как указано в EXIF
    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return source;

        int w = source.Width, h = source.Height;
        var result = IsRotated90(origin) ? new SKBitmap(h, w) : new SKBitmap(w, h);
        using var canvas = new SKCanvas(result);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // отражение по горизонтали
                canvas.Translate(w, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight: // поворот на 180°
                canvas.Translate(w, h);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft: // отражение по вертикали
                canvas.Translate(0, h);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop: // отражение по диагонали
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop: // поворот на 90° по часовой
                canvas.Translate(h, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom: // отражение по другой диагонали
                canvas.Translate(h, w);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom: // поворот на 90° против часовой
                canvas.Translate(0, w);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        return result;
    }
}
