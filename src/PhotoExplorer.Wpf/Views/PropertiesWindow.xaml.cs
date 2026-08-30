using System.Windows;
using PhotoExplorer.Core;
using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Wpf.Views;

// Окно свойств изображения: все сведения таблицей
public partial class PropertiesWindow : Window
{
    public PropertiesWindow(ImageFileInfo file)
    {
        InitializeComponent();
        Title = $"Свойства – {file.Name}";
        Table.ItemsSource = MetadataRows.Build(file, MetadataReader.Read(file.FullPath));
    }
}
