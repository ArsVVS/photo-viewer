using System.Windows;
using Microsoft.Win32;
using PhotoExplorer.Wpf.ViewModels;

namespace PhotoExplorer.Wpf.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(App.Settings);
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Стартовая папка" };
        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.StartFolder = dialog.FolderName;
            ViewModel.StartFromLastFolder = false;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var error = ViewModel.Validate();
        if (error != null)
        {
            MessageBox.Show(error, "Настройки", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ViewModel.ApplyTo(App.Settings);
        App.Settings.Save();
        DialogResult = true;
    }
}
