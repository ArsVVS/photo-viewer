using System.CommandLine;
using System.Text;
using PhotoExplorer.Cli;

Console.OutputEncoding = Encoding.UTF8;

var root = new RootCommand("PhotoExplorer – работа с папками изображений и кешем миниатюр");

// Глобальная опция: путь к другой базе кеша
var cacheOption = new Option<string?>("--cache")
{
    Description = "Путь к базе кеша миниатюр (по умолчанию %LOCALAPPDATA%/PhotoExplorer/thumbs.db)",
    Recursive = true
};
root.Options.Add(cacheOption);

// photos list <folder> [--sort] [--desc] [--format]
var listFolder = new Argument<string>("folder") { Description = "Папка с изображениями" };
var sortOption = new Option<string>("--sort") { Description = "Сортировка: name, date, size, type", DefaultValueFactory = _ => "name" };
sortOption.AcceptOnlyFromAmong("name", "date", "size", "type");
var descOption = new Option<bool>("--desc") { Description = "По убыванию" };
var formatOption = new Option<string>("--format") { Description = "Формат вывода: table или json", DefaultValueFactory = _ => "table" };
formatOption.AcceptOnlyFromAmong("table", "json");

var listCommand = new Command("list", "Список изображений в папке") { listFolder, sortOption, descOption, formatOption };
listCommand.SetAction(result => Commands.List(
    result.GetValue(listFolder)!, result.GetValue(sortOption)!, result.GetValue(descOption), result.GetValue(formatOption)!));
root.Subcommands.Add(listCommand);

// photos search <folder> --name ... [--recursive] [--type] [--from] [--to]
var searchFolder = new Argument<string>("folder") { Description = "Папка для поиска" };
var nameOption = new Option<string?>("--name") { Description = "Подстрока или маска имени (* и ?)" };
var recursiveOption = new Option<bool>("--recursive", "-r") { Description = "Искать во вложенных папках" };
var typeOption = new Option<string?>("--type") { Description = "Типы файлов через запятую, например jpg,png" };
var fromOption = new Option<DateTime?>("--from") { Description = "Дата изменения от (ГГГГ-ММ-ДД)" };
var toOption = new Option<DateTime?>("--to") { Description = "Дата изменения до (ГГГГ-ММ-ДД)" };

var searchCommand = new Command("search", "Поиск изображений") { searchFolder, nameOption, recursiveOption, typeOption, fromOption, toOption };
searchCommand.SetAction((result, token) => Commands.SearchAsync(
    result.GetValue(searchFolder)!, result.GetValue(nameOption), result.GetValue(recursiveOption),
    result.GetValue(typeOption), result.GetValue(fromOption), result.GetValue(toOption), token));
root.Subcommands.Add(searchCommand);

// photos info <file>
var infoFile = new Argument<string>("file") { Description = "Файл изображения" };
var infoCommand = new Command("info", "Размеры и EXIF изображения") { infoFile };
infoCommand.SetAction(result => Commands.Info(result.GetValue(infoFile)!));
root.Subcommands.Add(infoCommand);

return await root.Parse(args).InvokeAsync();
