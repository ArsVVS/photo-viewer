namespace PhotoExplorer.Core;

/// <summary>Вспомогательные методы для вывода значений пользователю.</summary>
public static class Formatting
{
    /// <summary>Размер в байтах в читаемом виде: 512 Б, 1,5 КБ, 3,2 МБ.</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} {units[0]}" : $"{value:0.#} {units[unit]}";
    }
}
