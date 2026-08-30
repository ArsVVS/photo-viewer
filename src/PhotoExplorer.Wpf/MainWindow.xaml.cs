using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PhotoExplorer.Core;
using PhotoExplorer.Wpf.ViewModels;
using PhotoExplorer.Wpf.Views;

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

    private void ThumbList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Двойной клик именно по плитке, а не по пустому месту
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(ThumbList, source) is ListBoxItem)
            OpenViewer();
    }

    private void ThumbList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            OpenViewer();
            e.Handled = true;
        }
        // Alt+Enter приходит как «системная» клавиша
        else if (e.Key == Key.System && e.SystemKey == Key.Enter)
        {
            ShowProperties();
            e.Handled = true;
        }
    }

    // Меню по пустому месту не показываем
    private void ThumbList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (ViewModel.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e) => OpenViewer();

    private void MenuRename_Click(object sender, RoutedEventArgs e) => RenameSelected();

    private void MenuDelete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private void MenuShowInExplorer_Click(object sender, RoutedEventArgs e)
    {
        var item = ViewModel.SelectedItem;
        if (item != null)
            Process.Start("explorer.exe", $"/select,\"{item.File.FullPath}\"");
    }

    private void MenuCopyPath_Click(object sender, RoutedEventArgs e)
    {
        var paths = ViewModel.SelectedItems.Select(i => i.File.FullPath);
        Clipboard.SetText(string.Join(Environment.NewLine, paths));
    }

    private void MenuProperties_Click(object sender, RoutedEventArgs e) => ShowProperties();

    // Окно свойств выбранного изображения
    public void ShowProperties()
    {
        var item = ViewModel.SelectedItem;
        if (item != null)
            new PropertiesWindow(item.File) { Owner = this }.ShowDialog();
    }

    // Переименование выбранного файла
    public void RenameSelected()
    {
        var item = ViewModel.SelectedItem;
        if (item == null)
            return;

        var dialog = new RenameWindow(item.Name) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.NewName == item.Name)
            return;

        try
        {
            var newPath = FileOperations.Rename(item.File.FullPath, dialog.NewName);
            ViewModel.Refresh();
            SelectFile(newPath);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "Переименовать", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // Удаление выбранных файлов в корзину (с подтверждением)
    public void DeleteSelected()
    {
        var items = ViewModel.SelectedItems.ToList();
        if (items.Count == 0)
            return;

        var question = items.Count == 1
            ? $"Удалить файл «{items[0].Name}» в корзину?"
            : $"Удалить выбранные файлы ({items.Count} шт.) в корзину?";
        if (MessageBox.Show(question, "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var deleted = new List<ThumbnailItemViewModel>();
        foreach (var item in items)
        {
            try
            {
                FileOperations.DeleteToRecycleBin(item.File.FullPath);
                deleted.Add(item);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                MessageBox.Show($"Не удалось удалить «{item.Name}»:\n{ex.Message}", "Удаление",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        ViewModel.RemoveItems(deleted);
    }

    // Выделяет файл в сетке и прокручивает к нему
    public void SelectFile(string path)
    {
        var item = ViewModel.FindItem(path);
        if (item == null)
            return;
        ThumbList.SelectedItem = item;
        ThumbList.ScrollIntoView(item);
    }

    // Открывает полноэкранный просмотр выбранного изображения
    public void OpenViewer()
    {
        var item = ThumbList.SelectedItem as ThumbnailItemViewModel;
        if (item == null)
            return;

        var items = ViewModel.Items.ToList();
        var viewer = new ViewerWindow(items.Select(i => i.File).ToList(), items.IndexOf(item)) { Owner = this };
        viewer.ShowDialog();

        // После выхода выделяем последнее просмотренное изображение
        var last = items[viewer.CurrentIndex];
        ThumbList.SelectedItem = last;
        ThumbList.ScrollIntoView(last);
        (ThumbList.ItemContainerGenerator.ContainerFromItem(last) as ListBoxItem)?.Focus();
    }
}
