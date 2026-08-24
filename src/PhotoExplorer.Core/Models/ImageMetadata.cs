namespace PhotoExplorer.Core.Models;

/// <summary>Сведения об изображении (разрешение, формат и EXIF).</summary>
public class ImageMetadata
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "";

    // Остальное есть не у всех файлов
    public DateTime? DateTaken { get; set; }
    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
    public string? ExposureTime { get; set; }
    public double? FNumber { get; set; }
    public int? Iso { get; set; }
    public int? Orientation { get; set; }

    /// <summary>Производитель и модель камеры одной строкой.</summary>
    public string? Camera
    {
        get
        {
            if (CameraMake == null && CameraModel == null)
                return null;
            // Часто модель уже начинается с названия производителя
            if (CameraMake != null && CameraModel != null && CameraModel.StartsWith(CameraMake, StringComparison.OrdinalIgnoreCase))
                return CameraModel;
            return $"{CameraMake} {CameraModel}".Trim();
        }
    }
}
