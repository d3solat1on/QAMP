using Windows.Media;

namespace QAMP.Services
{
    public partial class MediaControlsManager
    {
        // Используем MediaPlayer как "обертку-активатор" для шторки Windows 11
        private readonly global::Windows.Media.Playback.MediaPlayer? _dummyPlayer;
        private readonly SystemMediaTransportControls? _smtc;

        public event Action? OnPlayRequested;
        // public event Action? YouCanRemovePauseRequested; // Для совместимости с MainWindow
        public event Action? OnPauseRequested;
        public event Action? OnNextRequested;
        public event Action? OnPreviousRequested;

        private static void Log(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logLine = $"[{timestamp}] {message}";
                System.Diagnostics.Debug.WriteLine(logLine);
                AppDataManager.AppendSmtcLog(logLine);
            }
            catch { }
        }

        public MediaControlsManager(IntPtr windowHandle)
        {
            try
            {
                Log("[MEDIAPLAYER INIT] Создание фоновой аудиосессии...");

                // 1. Создаем пустой MediaPlayer. Он заставит Windows 11 выделить нам место в шторке!
                _dummyPlayer = new global::Windows.Media.Playback.MediaPlayer();
                _dummyPlayer.CommandManager.IsEnabled = true; // Разрешаем системный контроль

                // 2. Вытаскиваем из него встроенный SMTC (он уже правильно инициализирован внутри ОС)
                _smtc = _dummyPlayer.SystemMediaTransportControls;

                if (_smtc != null)
                {
                    // Включаем кнопки управления в шторке
                    _smtc.IsPlayEnabled = true;
                    _smtc.IsPauseEnabled = true;
                    _smtc.IsNextEnabled = true;
                    _smtc.IsPreviousEnabled = true;

                    // Подписываемся на события кликов в шторке
                    _smtc.ButtonPressed += Smtc_ButtonPressed;

                    Log("[MEDIAPLAYER SUCCESS] Windows 11 выделила аудиосессию для QAMP. Шторка создана!");
                }
                else
                {
                    Log("[MEDIAPLAYER ERROR] Не удалось получить SMTC из MediaPlayer.");
                }
            }
            catch (Exception ex)
            {
                Log($"[MEDIAPLAYER CRITICAL] Ошибка инициализации: {ex.Message}");
                Log($"[MEDIAPLAYER STACK] {ex.StackTrace}");
            }
        }

        private void Smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            Log($"[MEDIAPLAYER] Нажата кнопка в шторке Windows: {args.Button}");

            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    OnPlayRequested?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    OnPauseRequested?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    OnNextRequested?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    OnPreviousRequested?.Invoke();
                    break;
            }
        }

        public void UpdatePlaybackStatus(bool isPlaying)
        {
            if (_smtc == null) return;
            try
            {
                _smtc.PlaybackStatus = isPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
                Log($"[MEDIAPLAYER Status] Статус в шторке изменен на: {(isPlaying ? "Playing" : "Paused")}");
            }
            catch (Exception ex)
            {
                Log($"[MEDIAPLAYER ERROR] Не удалось обновить статус: {ex.Message}");
            }
        }

        public void UpdateTrackInfo(string? name, string? executor, string? album)
        {
            if (_smtc == null)
            {
                Log("[SMTC] SMTC is null, пропуск UpdateTrackInfo");
                return;
            }

            try
            {
                var updater = _smtc.DisplayUpdater;
                updater.Type = MediaPlaybackType.Music;

                // Устанавливаем текстовую информацию
                updater.MusicProperties.Title = name ?? "Unknown Track";
                updater.MusicProperties.Artist = executor ?? "Unknown Artist";
                updater.MusicProperties.AlbumTitle = album ?? "Unknown Album";

                Log($"[SMTC] Текстовая информация установлена: {name} - {executor}");


                // Синхронно меняем заголовок окна для Win32 (это дублирует имя в системе)
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.MainWindow?.Title = string.IsNullOrEmpty(executor)
                            ? $"{name} — QAMP"
                            : $"{executor} — {name} [QAMP]";
                });

                // Применяем все изменения в шторке
                updater.Update();
                Log("[SMTC] Update() вызван успешно");
            }
            catch (Exception ex)
            {
                Log($"[SMTC ERROR] Ошибка при обновлении информации: {ex.Message}");
            }
        }
    }
}