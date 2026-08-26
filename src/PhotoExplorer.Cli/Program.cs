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

// photos thumbs build <folder> [--recursive] [--size]
var buildFolder = new Argument<string>("folder") { Description = "Папка с изображениями" };
var buildRecursive = new Option<bool>("--recursive", "-r") { Description = "Включая вложенные папки" };
var buildSize = new Option<int>("--size") { Description = "Размер миниатюры по длинной стороне", DefaultValueFactory = _ => 160 };
var buildCommand = new Command("build", "Заранее построить миниатюры в кеш") { buildFolder, buildRecursive, buildSize };
buildCommand.SetAction(result => Commands.ThumbsBuildAsync(result.GetValue(cacheOption),
    result.GetValue(buildFolder)!, result.GetValue(buildRecursive), result.GetValue(buildSize)));

// photos thumbs export <file> --out thumb.jpg [--size]
var exportFile = new Argument<string>("file") { Description = "Файл изображения" };
var exportOut = new Option<string>("--out") { Description = "Куда сохранить миниатюру", Required = true };
var exportSize = new Option<int>("--size") { Description = "Размер миниатюры по длинной стороне", DefaultValueFactory = _ => 256 };
var exportCommand = new Command("export", "Сохранить миниатюру в файл") { exportFile, exportOut, exportSize };
exportCommand.SetAction(result => Commands.ThumbsExportAsync(result.GetValue(cacheOption),
    result.GetValue(exportFile)!, result.GetValue(exportOut)!, result.GetValue(exportSize)));

root.Subcommands.Add(new Command("thumbs", "Работа с миниатюрами") { buildCommand, exportCommand });

// photos cache stats | cleanup | clear
var statsCommand = new Command("stats", "Статистика кеша");
statsCommand.SetAction(result => Commands.CacheStats(result.GetValue(cacheOption)));
var cleanupCommand = new Command("cleanup", "Удалить записи о несуществующих файлах");
cleanupCommand.SetAction(result => Commands.CacheCleanup(result.GetValue(cacheOption)));
var clearCommand = new Command("clear", "Полностью очистить кеш");
clearCommand.SetAction(result => Commands.CacheClear(result.GetValue(cacheOption)));

root.Subcommands.Add(new Command("cache", "Работа с кешем миниатюр") { statsCommand, cleanupCommand, clearCommand });

return await root.Parse(args).InvokeAsync();
