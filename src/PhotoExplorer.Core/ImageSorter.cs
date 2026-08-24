using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Core;

public enum SortField { Name, Date, Size, Type }

/// <summary>Сортировка списка изображений.</summary>
public static class ImageSorter
{
    /// <summary>Возвращает новый отсортированный список.</summary>
    public static List<ImageFileInfo> Sort(IEnumerable<ImageFileInfo> files, SortField field, bool descending = false)
    {
        var list = files.ToList();
        list.Sort((a, b) =>
        {
            int result = field switch
            {
                SortField.Date => a.LastWriteTime.CompareTo(b.LastWriteTime),
                SortField.Size => a.Size.CompareTo(b.Size),
                SortField.Type => string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase),
                _ => 0
            };
            // При равенстве (и для сортировки по имени) сравниваем имена
            if (result == 0)
                result = CompareNatural(a.Name, b.Name);
            return descending ? -result : result;
        });
        return list;
    }

    /// <summary>Естественное сравнение строк: img2 меньше img10.</summary>
    public static int CompareNatural(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                // Вырезаем числа целиком
                int startA = i, startB = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                var numA = a[startA..i].TrimStart('0');
                var numB = b[startB..j].TrimStart('0');

                // Чем длиннее число без ведущих нулей, тем оно больше
                if (numA.Length != numB.Length)
                    return numA.Length.CompareTo(numB.Length);
                int cmp = string.CompareOrdinal(numA, numB);
                if (cmp != 0)
                    return cmp;
            }
            else
            {
                int cmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                if (cmp != 0)
                    return cmp;
                i++;
                j++;
            }
        }
        // Одна строка закончилась раньше – она меньше
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
