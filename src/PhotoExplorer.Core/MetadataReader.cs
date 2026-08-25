using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.FileType;
using PhotoExplorer.Core.Models;
using SkiaSharp;

namespace PhotoExplorer.Core;

/// <summary>Чтение размеров, формата и EXIF изображения.</summary>
public static class MetadataReader
{
    /// <summary>Читает сведения о файле. Если EXIF прочитать не удалось – заполняются только базовые поля.</summary>
    public static ImageMetadata Read(string path)
    {
        var result = new ImageMetadata
        {
            Format = Path.GetExtension(path).TrimStart('.').ToUpperInvariant()
        };

        // Разрешение берём через SkiaSharp – он понимает все наши форматы
        try
        {
            using var codec = SKCodec.Create(path);
            if (codec != null)
            {
                result.Width = codec.Info.Width;
                result.Height = codec.Info.Height;
            }
        }
        catch (Exception)
        {
            // Файл не читается – оставляем нули
        }

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);

            var fileType = directories.OfType<FileTypeDirectory>().FirstOrDefault();
            var typeName = fileType?.GetString(FileTypeDirectory.TagDetectedFileTypeName);
            if (!string.IsNullOrEmpty(typeName))
                result.Format = typeName;

            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 != null)
            {
                result.CameraMake = ifd0.GetString(ExifDirectoryBase.TagMake)?.Trim();
                result.CameraModel = ifd0.GetString(ExifDirectoryBase.TagModel)?.Trim();
                if (ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientation))
                    result.Orientation = orientation;
            }

            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd != null)
            {
                if (subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var date))
                    result.DateTaken = date;
                result.ExposureTime = subIfd.GetDescription(ExifDirectoryBase.TagExposureTime);
                if (subIfd.TryGetRational(ExifDirectoryBase.TagFNumber, out var fNumber))
                    result.FNumber = Math.Round(fNumber.ToDouble(), 1);
                if (subIfd.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out int iso))
                    result.Iso = iso;
            }
        }
        catch (Exception)
        {
            // EXIF не прочитался – остаются только разрешение и формат
        }

        // Повёрнутое фото показываем с «правильными» шириной и высотой
        if (result.Orientation is >= 5 and <= 8)
            (result.Width, result.Height) = (result.Height, result.Width);

        return result;
    }
}
