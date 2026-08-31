using System.Windows;

namespace PhotoExplorer.Wpf.Views;

// Окно справки (F1): горячие клавиши и параметры CLI
public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();

        MainKeys.ItemsSource = new List<InfoRow>
        {
            new("F1", "эта справка"),
            new("Enter / двойной клик", "полноэкранный просмотр"),
            new("Alt+← / Alt+→", "назад / вперёд по истории папок"),
            new("Backspace", "на папку выше"),
            new("Ctrl+F", "перейти к полю поиска"),
            new("Ctrl+1 / 2 / 3", "малые / средние / крупные миниатюры"),
            new("F2", "переименовать файл"),
            new("Delete", "удалить в корзину (с подтверждением)"),
            new("Alt+Enter", "свойства изображения"),
            new("Ctrl+A", "выделить все"),
            new("F5", "обновить папку"),
            new("Ctrl+D", "добавить папку в избранное"),
            new("Ctrl / Shift + клик", "выделить несколько файлов")
        };

        ViewerKeys.ItemsSource = new List<InfoRow>
        {
            new("← / →", "предыдущее / следующее изображение"),
            new("Колесо мыши", "масштаб относительно курсора"),
            new("Перетаскивание мышью", "сдвиг изображения"),
            new("0", "вписать в экран"),
            new("1", "масштаб 100%"),
            new("R / L", "повернуть по / против часовой (файл не меняется)"),
            new("I", "показать / скрыть информацию и EXIF"),
            new("Space", "слайд-шоу / пауза"),
            new("Esc", "выход из просмотра")
        };

        CliCommands.ItemsSource = new List<InfoRow>
        {
            new("list <папка>", "список изображений; --sort name|date|size|type, --desc, --format table|json"),
            new("search <папка>", "поиск; --name \"*маска*\", --recursive, --type jpg,png, --from 2026-01-01, --to 2026-08-31"),
            new("info <файл>", "размеры и EXIF"),
            new("thumbs build <папка>", "заранее построить миниатюры в кеш; --recursive, --size 160"),
            new("thumbs export <файл>", "сохранить миниатюру; --out thumb.jpg, --size 256"),
            new("cache stats", "статистика кеша"),
            new("cache cleanup", "удалить записи о несуществующих файлах"),
            new("cache clear", "очистить кеш"),
            new("--help", "справка по командам")
        };
    }
}
