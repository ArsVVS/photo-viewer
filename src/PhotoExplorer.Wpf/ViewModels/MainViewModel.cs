using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoExplorer.Core;
using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FolderBrowser _browser;

    // Корни дерева папок
    public ObservableCollection<FolderItemViewModel> RootItems { get; } = [];

    // Файлы текущей папки
    public ObservableCollection<ImageFileInfo> Items { get; } = [];

    [ObservableProperty]
    public partial string CurrentFolder { get; set; } = "";

    public MainViewModel()
    {
        _browser = new FolderBrowser(App.Settings);
        LoadTree();
    }

    // Строит дерево: «Этот компьютер» со списком дисков
    private void LoadTree()
    {
        RootItems.Clear();
        var computer = new FolderItemViewModel("Этот компьютер", "", Icons.Computer, null) { IsExpanded = true };
        foreach (var drive in _browser.GetDrives())
            computer.Children.Add(new FolderItemViewModel(drive.Name, drive.Path, Icons.Drive, _browser, drive.HasSubfolders));
        RootItems.Add(computer);
    }

    // Открывает папку
    public void NavigateTo(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        CurrentFolder = Path.GetFullPath(path);
        App.Settings.LastFolder = CurrentFolder;

        Items.Clear();
        foreach (var file in ImageSorter.Sort(_browser.GetImages(CurrentFolder), SortField.Name))
            Items.Add(file);
    }
}
