using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoExplorer.Core;

namespace PhotoExplorer.Wpf.ViewModels;

// Узел дерева папок. Подпапки загружаются только при раскрытии.
public partial class FolderItemViewModel : ObservableObject
{
    // Заглушка, чтобы у нераскрытой папки была стрелка
    private static readonly FolderItemViewModel Placeholder = new("", "", "", null);

    private readonly FolderBrowser? _browser;

    public string Name { get; }
    public string Path { get; }
    public string Icon { get; }
    public ObservableCollection<FolderItemViewModel> Children { get; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public FolderItemViewModel(string name, string path, string icon, FolderBrowser? browser, bool hasSubfolders = false)
    {
        Name = name;
        Path = path;
        Icon = icon;
        _browser = browser;
        if (hasSubfolders)
            Children.Add(Placeholder);
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && Children.Count == 1 && Children[0] == Placeholder)
            LoadChildren();
    }

    // Перечитывает подпапки
    public void LoadChildren()
    {
        if (_browser == null || Path == "")
            return;

        Children.Clear();
        foreach (var folder in _browser.GetSubfolders(Path))
            Children.Add(new FolderItemViewModel(folder.Name, folder.Path, Icons.Folder, _browser, folder.HasSubfolders));
    }
}
