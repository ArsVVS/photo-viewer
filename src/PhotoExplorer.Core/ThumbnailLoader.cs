using PhotoExplorer.Core.Models;

namespace PhotoExplorer.Core;

/// <summary>Фоновая загрузка миниатюр для списка файлов.</summary>
public class ThumbnailLoader(ThumbnailCache cache)
{
    // Сколько миниатюр строится одновременно
    private const int MaxParallel = 4;

    private CancellationTokenSource? _cts;

    /// <summary>Миниатюра готова: путь к файлу и JPEG-байты (null – файл не удалось прочитать).</summary>
    public event Action<string, byte[]?>? ThumbnailReady;

    /// <summary>Прогресс загрузки: сколько готово и сколько всего.</summary>
    public event Action<int, int>? ProgressChanged;

    /// <summary>Загружает миниатюры. Предыдущая незавершённая загрузка отменяется.</summary>
    public async Task LoadAsync(IReadOnlyList<ImageFileInfo> files, int size)
    {
        Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;

        int done = 0;
        ProgressChanged?.Invoke(0, files.Count);

        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxParallel, CancellationToken = cts.Token };
        try
        {
            await Parallel.ForEachAsync(files, options, async (file, token) =>
            {
                ThumbnailResult? thumb = null;
                try
                {
                    thumb = await cache.GetOrCreateAsync(file.FullPath, size, token);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // Ошибка с одним файлом не должна останавливать остальные
                }

                if (thumb != null)
                {
                    file.Width = thumb.OriginalWidth;
                    file.Height = thumb.OriginalHeight;
                }

                token.ThrowIfCancellationRequested();
                ThumbnailReady?.Invoke(file.FullPath, thumb?.Data);
                ProgressChanged?.Invoke(Interlocked.Increment(ref done), files.Count);
            });
        }
        catch (OperationCanceledException)
        {
            // Загрузку отменили – это нормально
        }
    }

    /// <summary>Отменяет текущую загрузку.</summary>
    public void Cancel()
    {
        _cts?.Cancel();
        _cts = null;
    }
}
