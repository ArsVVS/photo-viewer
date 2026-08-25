using Microsoft.Data.Sqlite;

namespace PhotoExplorer.Core;

/// <summary>Кеш миниатюр в базе SQLite.</summary>
public class ThumbnailCache
{
    private readonly string _connectionString;

    /// <summary>Путь к файлу базы.</summary>
    public string DatabasePath { get; }

    /// <summary>Путь к базе по умолчанию.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoExplorer", "thumbs.db");

    /// <summary>Открывает (или создаёт) базу кеша.</summary>
    public ThumbnailCache(string? databasePath = null)
    {
        DatabasePath = Path.GetFullPath(databasePath ?? DefaultPath);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();

        using var connection = Open();
        Execute(connection, "PRAGMA journal_mode=WAL;");
        Execute(connection, """
            CREATE TABLE IF NOT EXISTS thumbnails (
                path          TEXT    NOT NULL COLLATE NOCASE,
                thumb_size    INTEGER NOT NULL,
                file_size     INTEGER NOT NULL,
                file_modified INTEGER NOT NULL,
                width         INTEGER NOT NULL,
                height        INTEGER NOT NULL,
                data          BLOB    NOT NULL,
                last_access   INTEGER NOT NULL,
                PRIMARY KEY (path, thumb_size)
            );
            """);
    }

    /// <summary>Берёт миниатюру из кеша или строит новую и сохраняет её.</summary>
    public async Task<ThumbnailResult?> GetOrCreateAsync(string path, int size, CancellationToken token = default)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
            return null;

        long fileSize = file.Length;
        long modified = file.LastWriteTimeUtc.Ticks;
        string fullPath = file.FullName;

        // Если миниатюра уже есть в базе и файл не менялся – берём её оттуда
        var cached = await Task.Run(() => TryGet(fullPath, size, fileSize, modified), token);
        if (cached != null)
            return cached;

        // Иначе строим заново
        var thumb = await Task.Run(() => ThumbnailGenerator.Generate(fullPath, size), token);
        if (thumb != null)
            await Task.Run(() => Save(fullPath, size, fileSize, modified, thumb), token);
        return thumb;
    }

    private ThumbnailResult? TryGet(string path, int size, long fileSize, long modified)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT width, height, data FROM thumbnails
            WHERE path = $path AND thumb_size = $size AND file_size = $fileSize AND file_modified = $modified
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$size", size);
        command.Parameters.AddWithValue("$fileSize", fileSize);
        command.Parameters.AddWithValue("$modified", modified);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        var result = new ThumbnailResult((byte[])reader["data"], reader.GetInt32(0), reader.GetInt32(1));
        reader.Close();

        // Запоминаем, когда миниатюру открывали последний раз
        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE thumbnails SET last_access = $now WHERE path = $path AND thumb_size = $size";
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.Ticks);
        update.Parameters.AddWithValue("$path", path);
        update.Parameters.AddWithValue("$size", size);
        update.ExecuteNonQuery();

        return result;
    }

    private void Save(string path, int size, long fileSize, long modified, ThumbnailResult thumb)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        // INSERT OR REPLACE заменит устаревшую запись, если она была
        command.CommandText = """
            INSERT OR REPLACE INTO thumbnails (path, thumb_size, file_size, file_modified, width, height, data, last_access)
            VALUES ($path, $size, $fileSize, $modified, $width, $height, $data, $now)
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$size", size);
        command.Parameters.AddWithValue("$fileSize", fileSize);
        command.Parameters.AddWithValue("$modified", modified);
        command.Parameters.AddWithValue("$width", thumb.OriginalWidth);
        command.Parameters.AddWithValue("$height", thumb.OriginalHeight);
        command.Parameters.AddWithValue("$data", thumb.Data);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.Ticks);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
