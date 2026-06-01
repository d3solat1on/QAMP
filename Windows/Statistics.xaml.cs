using System.Windows;
using QAMP.Services;
namespace QAMP.Windows;

public partial class Statistics : Window
{
    private System.Threading.Timer? _debounceTimer;
    private const int DebounceDelayMs = 1000; // Обновлять не чаще чем раз в секунду

    public Statistics()
    {        
        InitializeComponent();
        Loaded += async (s, e) => await RefreshAllStatisticsAsync();
        Loaded += (s, e) => SubscribeToStatisticsChanges();
        Closed += (s, e) => UnsubscribeFromStatisticsChanges();
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }

    /// <summary>
    /// Подписывается на событие изменения статистики
    /// </summary>
    private void SubscribeToStatisticsChanges()
    {
        DatabaseService.StatisticsChanged += OnStatisticsChanged;
        System.Diagnostics.Debug.WriteLine("Statistics окно: подписано на событие StatisticsChanged");
    }

    /// <summary>
    /// Отписывается от события изменения статистики
    /// </summary>
    private void UnsubscribeFromStatisticsChanges()
    {
        DatabaseService.StatisticsChanged -= OnStatisticsChanged;
        _debounceTimer?.Dispose();
        System.Diagnostics.Debug.WriteLine("Statistics окно: отписано от события StatisticsChanged");
    }

    /// <summary>
    /// Обработчик события изменения статистики с debouncing
    /// </summary>
    private void OnStatisticsChanged()
    {
        // Отменяем предыдущий timer
        _debounceTimer?.Dispose();

        // Запускаем новый timer с задержкой
        _debounceTimer = new Timer(async (_) =>
        {
            System.Diagnostics.Debug.WriteLine("Statistics окно: debounce завершен, обновляю данные...");
            await Dispatcher.BeginInvoke(async () => await RefreshAllStatisticsAsync());
        }, null, DebounceDelayMs, Timeout.Infinite);
    }
    public async Task RefreshAllStatisticsAsync()
    {
        var playlistCount = await Task.Run(() => DatabaseService.GetPlaylistCount().ToString());
        var trackCount = await Task.Run(() => DatabaseService.GetTrackCount().ToString());
        var mostListened = await Task.Run(() => DatabaseService.GetMostListenedTracks());
        var hiResKing = await Task.Run(() => DatabaseService.GetHiResKing());
        var longestTrack = await Task.Run(() => DatabaseService.GetLongestTrack());
        var shortestTrack = await Task.Run(() => DatabaseService.GetShortestTrack());
        var totalLibrarySize = await Task.Run(() => DatabaseService.GetTotalLibrarySize());
        var totalLibraryWeight = await Task.Run(() => DatabaseService.GetTotalLibraryWeight());
        var mostListenedArtistText = await Task.Run(() => DatabaseService.GetMostListenedArtist());
        var tracksWithoutListening = await Task.Run(() => DatabaseService.GetTracksWithoutListnenig());
        TracksWithoutListening.Text = $"Треки без прослушивания \n{tracksWithoutListening}";
        MostListenedArtistText.Text = $"Самый прослушиваемый исполнитель: {mostListenedArtistText}";
        PlaylistCountText.Text = playlistCount;
        TrackCountText.Text = trackCount;
        MostListenedTrackText.Text = mostListened;
        HighestBitrateText.Text = hiResKing;
        LongestTrackText.Text = longestTrack;
        ShortestTrackText.Text = shortestTrack;
        TotalLibrarySizeText.Text = totalLibrarySize;
        TotalLibraryWeightText.Text = totalLibraryWeight;
    }
    public void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}