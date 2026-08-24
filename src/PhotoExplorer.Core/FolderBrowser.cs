using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Core;

/// <summary>Получение списка дисков, папок и изображений.</summary>
public class FolderBrowser(AppSettings settings)
{
    /// <summary>Список готовых к работе дисков.</summary>
    public List<FolderNode> GetDrives()
    {
        var result = new List<FolderNode>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
                continue;

            var name = drive.Name.TrimEnd(Path.DirectorySeparatorChar);
            try
            {
                if (!string.IsNullOrEmpty(drive.VolumeLabel))
                    name = $"{drive.VolumeLabel} ({name})";
            }
            catch (Exception)
            {
                // Метку диска прочитать не удалось – оставляем просто букву
            }
            result.Add(new FolderNode(drive.RootDirectory.FullName, name, true));
        }
        return result;
    }

    /// <summary>Подпапки указанной папки. Недоступные папки пропускаются.</summary>
    public List<FolderNode> GetSubfolders(string path)
    {
        var result = new List<FolderNode>();
        foreach (var dir in SafeGetDirectories(new DirectoryInfo(path)))
        {
            var hasSubfolders = SafeGetDirectories(dir).Any();
            result.Add(new FolderNode(dir.FullName, dir.Name, hasSubfolders));
        }
        return result.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Поддерживаемые изображения в папке (без вложенных).</summary>
    public List<ImageFileInfo> GetImages(string path)
    {
        var result = new List<ImageFileInfo>();
        try
        {
            foreach (var file in new DirectoryInfo(path).EnumerateFiles())
            {
                if (settings.IsSupported(file.Name))
                    result.Add(ImageFileInfo.FromFileInfo(file));
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            // Нет доступа к папке – возвращаем то, что успели прочитать
        }
        return result;
    }

    // Перечисляет подпапки, пропуская скрытые/системные (если так настроено) и недоступные
    private IEnumerable<DirectoryInfo> SafeGetDirectories(DirectoryInfo dir)
    {
        DirectoryInfo[] dirs;
        try
        {
            dirs = dir.GetDirectories();
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            yield break;
        }

        foreach (var d in dirs)
        {
            if (!settings.ShowHiddenFolders && (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                continue;
            yield return d;
        }
    }
}
