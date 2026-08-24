namespace PhotoExplorer.Core.Models;

/// <summary>Папка для дерева папок.</summary>
public record FolderNode(string Path, string Name, bool HasSubfolders);
