using Microsoft.Data.Sqlite;

namespace PhotoExplorer.Core;

/// <summary>Кеш миниатюр в базе SQLite.</summary>
public class ThumbnailCache
{
    private readonly string _connectionString;
    private int _hits;
    private int _misses;

    /// <summary>Сколько раз миниатюра нашлась в кеше за сессию.</summary>
    public int Hits => _hits;

    /// <summary>Сколько раз миниатюру пришлось строить за сессию.</summary>
    public int Misses => _misses;

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
        {
            Interlocked.Increment(ref _hits);
            return cached;
        }

        // Иначе строим заново
        Interlocked.Increment(ref _misses);
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

    /// <summary>Статистика: число записей, размер базы и попадания за сессию.</summary>
    public CacheStats GetStats()
    {
        using var connection = Open();
        long count = Scalar(connection, "SELECT COUNT(*) FROM thumbnails");
        long pages = Scalar(connection, "PRAGMA page_count");
        long pageSize = Scalar(connection, "PRAGMA page_size");
        return new CacheStats(count, pages * pageSize, Hits, Misses);
    }

    /// <summary>Удаляет записи о файлах, которых больше нет. Возвращает число удалённых записей.</summary>
    public int RemoveMissing()
    {
        using var connection = Open();

        var missing = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT DISTINCT path FROM thumbnails";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var path = reader.GetString(0);
                if (!File.Exists(path))
                    missing.Add(path);
            }
        }

        int removed = 0;
        foreach (var path in missing)
        {
            using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM thumbnails WHERE path = $path";
            delete.Parameters.AddWithValue("$path", path);
            removed += delete.ExecuteNonQuery();
        }

        if (removed > 0)
            Execute(connection, "VACUUM");
        return removed;
    }

    /// <summary>Оставляет в кеше не больше maxBytes данных, удаляя давно не открывавшиеся записи.</summary>
    public int TrimToSize(long maxBytes)
    {
        using var connection = Open();

        // Идём от самых свежих записей к старым и считаем, сколько уже набралось
        var toDelete = new List<(string Path, long Size)>();
        long total = 0;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT path, thumb_size, length(data) FROM thumbnails ORDER BY last_access DESC";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                total += reader.GetInt64(2);
                if (total > maxBytes)
                    toDelete.Add((reader.GetString(0), reader.GetInt64(1)));
            }
        }

        foreach (var (path, size) in toDelete)
        {
            using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM thumbnails WHERE path = $path AND thumb_size = $size";
            delete.Parameters.AddWithValue("$path", path);
            delete.Parameters.AddWithValue("$size", size);
            delete.ExecuteNonQuery();
        }

        // VACUUM уменьшает сам файл базы после удаления
        if (toDelete.Count > 0)
            Execute(connection, "VACUUM");
        return toDelete.Count;
    }

    /// <summary>Полностью очищает кеш.</summary>
    public void Clear()
    {
        using var connection = Open();
        Execute(connection, "DELETE FROM thumbnails");
        Execute(connection, "VACUUM");
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

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }
}

/// <summary>Статистика кеша.</summary>
public record CacheStats(long Count, long DatabaseBytes, int Hits, int Misses)
{
    /// <summary>Процент попаданий в кеш за сессию.</summary>
    public double HitRate => Hits + Misses == 0 ? 0 : 100.0 * Hits / (Hits + Misses);
}
