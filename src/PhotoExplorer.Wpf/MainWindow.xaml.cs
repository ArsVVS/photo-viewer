using System.Windows;
using System.Windows.Controls;
using PhotoExplorer.Wpf.ViewModels;

namespace PhotoExplorer.Wpf;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ViewModel.OpenStartFolder();
    }

    // Выбрали папку в дереве – открываем её
    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderItemViewModel folder && folder.Path != "")
            ViewModel.NavigateTo(folder.Path);
    }

    // SelectedItems у ListBox нельзя привязать напрямую, поэтому передаём вручную
    private void ThumbList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetSelection(ThumbList.SelectedItems.Cast<ThumbnailItemViewModel>().ToList());
    }
}
