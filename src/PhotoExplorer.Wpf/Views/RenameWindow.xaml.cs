using System.IO;
using System.Windows;
using PhotoExplorer.Core;

namespace PhotoExplorer.Wpf.Views;

// Простое окно для ввода нового имени файла
public partial class RenameWindow : Window
{
    public string NewName => NameBox.Text.Trim();

    public RenameWindow(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        Loaded += (_, _) =>
        {
            // Выделяем имя без расширения, как в проводнике
            NameBox.Focus();
            NameBox.Select(0, Path.GetFileNameWithoutExtension(currentName).Length);
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!FileOperations.IsValidFileName(NewName))
        {
            MessageBox.Show("Имя файла пустое или содержит недопустимые символы: \\ / : * ? \" < > |",
                "Переименовать", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
}
