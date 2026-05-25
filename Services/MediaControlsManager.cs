using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QAMP.Services
{
    public partial class MediaControlsManager
    {
        private static readonly string LogFilePath = AppDataManager.SmtcLogPath;

        // Используем MediaPlayer как "обертку-активатор" для шторки Windows 11
        private readonly global::Windows.Media.Playback.MediaPlayer? _dummyPlayer;
        private readonly SystemMediaTransportControls? _smtc;

        // Храним поток обложки, чтобы ОС могла асинхронно читать данные
        private MemoryStream? _coverMemoryStream;
        private IRandomAccessStream? _coverWinrtStream;
        private RandomAccessStreamReference? _thumbnailReference;

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

        private static byte[] ProcessCoverImage(byte[] input)
        {
            try
            {
                const int maxBytes = 200 * 1024; // 200 KB target
                const int maxDim = 600; // max width/height

                // If already small JPEG, return as-is
                if (input.Length <= maxBytes && input.Length > 2 && input[0] == 0xFF && input[1] == 0xD8)
                {
                    return input;
                }

                // Load into BitmapImage
                BitmapImage bmp = new();
                using (var ms = new MemoryStream(input))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                }

                int width = bmp.PixelWidth;
                int height = bmp.PixelHeight;
                double scale = 1.0;
                int longest = Math.Max(width, height);
                if (longest > maxDim) scale = (double)maxDim / longest;

                BitmapSource source = bmp;
                if (scale < 1.0)
                {
                    var tb = new TransformedBitmap(bmp, new ScaleTransform(scale, scale));
                    tb.Freeze();
                    source = tb;
                }

                // Try several quality levels until size under threshold
                for (int quality = 90; quality >= 50; quality -= 10)
                {
                    var encoder = new JpegBitmapEncoder
                    {
                        QualityLevel = quality
                    };
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    using var outMs = new MemoryStream();
                    encoder.Save(outMs);
                    if (outMs.Length <= maxBytes || quality == 50)
                    {
                        return outMs.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[SMTC WARNING] ProcessCoverImage failed: {ex.Message}");
            }

            // fallback: return original
            return input;
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

        public void UpdateTrackInfo(string? name, string? executor, string? album, byte[]? CoverImage)
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
                updater.MusicProperties.Title = name ?? "Неизвестный трек";
                updater.MusicProperties.Artist = executor ?? "Неизвестный исполнитель";
                updater.MusicProperties.AlbumTitle = album ?? "Неизвестный альбом";

                Log($"[SMTC] Текстовая информация установлена: {name} - {executor}");

                // Обрабатываем обложку (Thumbnail)
                if (CoverImage != null && CoverImage.Length > 0)
                {
                    try
                    {
                        // Преобразуем/сжимаем изображение при необходимости
                        var processed = ProcessCoverImage(CoverImage);

                        // Сохраняем поток в поле и НЕ закрываем его — Windows читает его асинхронно
                        _coverMemoryStream = new MemoryStream(processed);
                        _coverWinrtStream = _coverMemoryStream.AsRandomAccessStream();
                        _thumbnailReference = RandomAccessStreamReference.CreateFromStream(_coverWinrtStream);
                        updater.Thumbnail = _thumbnailReference;
                        Log($"[SMTC] Обложка успешно передана в шторку (оригинал: {CoverImage.Length} байт, передано: {processed.Length} байт)");
                    }
                    catch (Exception ex)
                    {
                        Log($"[SMTC WARNING] Не удалось загрузить обложку: {ex.Message}");
                        updater.Thumbnail = null;
                    }
                }
                else
                {
                    updater.Thumbnail = null;
                    Log("[SMTC] Обложка отсутствует, сброс на дефолтную иконку");
                }

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