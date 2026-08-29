using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
