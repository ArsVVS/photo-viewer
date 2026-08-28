using System.Windows;
using PhotoExplorer.Wpf.ViewModels;

namespace PhotoExplorer.Wpf;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
    }

    // Выбрали папку в дереве – открываем её
    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderItemViewModel folder && folder.Path != "")
            ViewModel.NavigateTo(folder.Path);
    }
}
