namespace QAMP;

public class StressTester
{
    private CancellationTokenSource? _cts;
    // Делегаты для управления плеером извне
    private readonly Action _togglePlayPause;
    private readonly Action<int> _setPlaylistIndex;
    private readonly Func<int> _getPlaylistsCount;
    private readonly Func<int> _getTracksCount;
    private readonly Action<int> _playTrackByIndex;
    private readonly Action<string> _showMessage;

    public StressTester(
        Action togglePlayPause,
        Action<int> setPlaylistIndex,
        Func<int> getPlaylistsCount,
        Func<int> getTracksCount,
        Action<int> playTrackByIndex,
        Action<string> showMessage)
    {
        _togglePlayPause = togglePlayPause;
        _setPlaylistIndex = setPlaylistIndex;
        _getPlaylistsCount = getPlaylistsCount;
        _getTracksCount = getTracksCount;
        _playTrackByIndex = playTrackByIndex;
        _showMessage = showMessage;
    }

    public bool IsRunning => _cts != null;

    public async Task Run(TimeSpan duration)
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        App.LogInfo("=== ЗАПУСК СТРЕСС-ТЕСТА (Action-based) ===");
        _showMessage("Стресс-тест запущен на 5 минут");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Random rnd = new();

        try
        {
            while (sw.Elapsed < duration && !_cts.Token.IsCancellationRequested)
            {
                // 1. Смена плейлиста
                int pCount = _getPlaylistsCount();
                if (pCount > 0)
                {
                    _setPlaylistIndex(rnd.Next(pCount));
                }

                await Task.Delay(rnd.Next(200, 500), _cts.Token);

                // 2. Запуск случайного трека
                int tCount = _getTracksCount();
                if (tCount > 0)
                {
                    _playTrackByIndex(rnd.Next(tCount));
                }

                await Task.Delay(rnd.Next(500, 1500), _cts.Token);

                // 3. Пауза/Воспроизведение
                _togglePlayPause();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            Stop();
            App.LogInfo("=== СТРЕСС-ТЕСТ ЗАВЕРШЕН ===");
            _showMessage("Стресс-тест завершен");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }
}
