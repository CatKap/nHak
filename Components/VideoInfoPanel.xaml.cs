using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.Win32;
using System.Windows.Input;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace EmotionAnalyzer.Components
{

    public partial class VideoInfoPanel : UserControl
    {
        private string uploadUrl = "https://rewrd.ru/files/upload";
        private MediaElement? _mediaPlayer;
        private DispatcherTimer? _progressTimer;
        private bool _isPlaying = false;
        private bool _isFullscreen = false;
        private string _currentVideoPath = string.Empty;
        private string _currentVideoName = string.Empty;
        private Window? _fullscreenWindow;

        // Статистика пользовательских действий
        private List<UserAction> _userActions = new List<UserAction>();
        private DateTime _sessionStartTime;
        private bool _isCollectingStats = true;

        // Для BrainBit анализа
        private BrainBitManager? _brainBitManager;
        private List<BrainBitManager.AnalysisData> _brainBitData = new List<BrainBitManager.AnalysisData>();
        private bool _isBrainBitAnalyzing = false;
        private bool _isWaitingForBrainBitData = false;
        private BrainBitManager.AnalysisData? _firstBrainBitData = null;
        // Класс для хранения действий пользователя
        public class UserAction
        {
            public DateTime Timestamp { get; set; }
            public string ActionType { get; set; } = string.Empty; // "play", "pause", "rewind", "forward", "seek"
            public TimeSpan VideoPosition { get; set; }
            public object? AdditionalData { get; set; } // Дополнительные данные

            public override string ToString()
            {
                return $"{Timestamp:HH:mm:ss} - {ActionType} at {VideoPosition:mm\\:ss}";
            }
        }
        
        public void RunAndForget(Func<Task> asyncAction)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await asyncAction().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Log error appropriately for your application
                    Console.WriteLine($"Background task failed: {ex.Message}");
                }
            });
        }
        

        public VideoInfoPanel()
        {
            InitializeComponent();
            InitializeVideoPlayer();
            InitializeTimer();
            InitializeEventHandlers();

            // Начинаем сбор статистики
            StartStatisticsCollection();

            // Инициализируем BrainBitManager
            _brainBitManager = new BrainBitManager();
            _brainBitManager.AnalysisDataReceived += OnBrainBitDataReceived;
            _brainBitManager.AnalysisStateChanged += OnBrainBitStateChanged;

            // Подписываемся на обновление данных в реальном времени
            _brainBitManager.AnalysisDataReceived += OnBrainBitDataForRealTime;
        }

// Новый обработчик для обновления данных в реальном времени
        

// Метод для обновления графиков
        private void UpdateRealTimeCharts(BrainBitManager.AnalysisData data)
        {
            // Получаем ссылки на панели через родительское окно
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;

            // Обновляем EngagementPanel
            if (mainWindow.EngagementPanelComponent != null)
            {
                mainWindow.EngagementPanelComponent.UpdateEegData(
                    data.Alpha,           // альфа-волны
                    data.Beta,            // бета-волны
                    data.Gamma,           // гамма-волны
                    data.Theta,           // тета-волны
                    data.Delta,           // дельта-волны
                    data.VideoTime
                );
            }

            // Обновляем EEGPanel
            if (mainWindow.EEGPanelComponent != null)
            {
                mainWindow.EEGPanelComponent.UpdateEEGData(
                    data.Alpha,
                    data.Beta,
                    data.Gamma,
                    data.Theta,
                    data.Delta,
                    data.InstAttention,
                    data.InstRelaxation
                );
            }
        }

        private void StartStatisticsCollection()
        {
            _sessionStartTime = DateTime.Now;
            _userActions.Clear();

            // Записываем начальное действие
            LogAction("session_start", TimeSpan.Zero, "Начало сессии просмотра");
        }

        private void LogAction(string actionType, TimeSpan position, object? additionalData = null)
        {
            if (!_isCollectingStats) return;

            var action = new UserAction
            {
                Timestamp = DateTime.Now,
                ActionType = actionType,
                VideoPosition = position,
                AdditionalData = additionalData
            };

            _userActions.Add(action);
            Console.WriteLine($"Действие записано: {action}");
        }

        private void InitializeVideoPlayer()
        {
            _mediaPlayer = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Stretch = Stretch.Uniform,
                ScrubbingEnabled = true,
                Volume = 1.0,
                IsMuted = false
            };

            VideoPlayerContainer.Child = _mediaPlayer;
        }

        private void InitializeTimer()
        {
            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _progressTimer.Tick += UpdateVideoProgress;
        }

        private void InitializeEventHandlers()
        {
            if (_mediaPlayer != null)
            {
                PlayPauseButton.Click += OnPlayPauseClicked;
                Rewind5Button.Click += OnRewindClicked;
                Forward5Button.Click += OnForwardClicked;
                FullscreenButton.Click += OnFullscreenClicked;

                // Заменяем Heatmap на Statistics
                StatisticsButton.Click += OnStatisticsClicked;
                UploadVideoButton.Click += OnUploadVideoClicked;

                _mediaPlayer.MediaOpened += OnMediaOpened;
                _mediaPlayer.MediaEnded += OnMediaEnded;
                _mediaPlayer.MediaFailed += OnMediaFailed;
            }
        }

        public void LoadVideo(string videoPath)
        {
            try
            {
                if (_mediaPlayer == null || string.IsNullOrEmpty(videoPath))
                    return;

                if (!File.Exists(videoPath))
                {
                    MessageBox.Show($"Файл не найден: {videoPath}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _currentVideoPath = videoPath;
                
        
                _currentVideoName = $"{DateTime.Now}_{Path.GetFileName(_currentVideoPath)}";

                var fileUploadUrl = $"{uploadUrl}/{_currentVideoName}";
                using (var client = new HttpClient())
                using (var fileStream = File.OpenRead(_currentVideoPath))
                {
                    var content = new StreamContent(fileStream);
                    RunAndForget(() => client.PutAsync(uploadUrl, content));
                }
                
                // Сбрасываем статистику для нового видео
                ResetStatisticsForNewVideo();
        
                if (_isPlaying)
                {
                    _mediaPlayer.Stop();
                    _progressTimer?.Stop();
                    _isPlaying = false;
                }

                // Загружаем видео, но НЕ воспроизводим сразу
                _mediaPlayer.Source = new Uri(videoPath, UriKind.Absolute);
                _mediaPlayer.Stop(); // Останавливаем сразу
        
                _isPlaying = false;
                _isWaitingForBrainBitData = false; // Пока не ждем данные
                UpdatePlayPauseButton();
        
                UpdateVideoInfo(videoPath);
                VideoTimestamp.Text = "00:00 / 00:00";
        
                // Логируем загрузку видео
                LogAction("video_load", TimeSpan.Zero, Path.GetFileName(videoPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке видео: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LogDebug(string message)
        {
            Console.WriteLine($"[VideoInfoPanel] {DateTime.Now:HH:mm:ss} {message}");
        }
        
        private bool IsBrainBitDataValid(BrainBitManager.AnalysisData data)
        {
            // Проверяем, что получены осмысленные данные
            // (не нули и не начальные калибровочные значения)
            return data.Alpha > 0.1 || 
                   data.Beta > 0.1 || 
                   data.InstAttention > 0.1 ||
                   data.InstRelaxation > 0.1;
        }
        private void OnBrainBitDataForRealTime(BrainBitManager.AnalysisData data)
        {
            // Передаем данные в менеджер
            RealTimeDataManager.Instance.AddBrainBitData(data);
    
            // Обновляем интерфейс через Dispatcher
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Отправляем данные на обновление графиков
                UpdateRealTimeCharts(data);
            });
        }
        private void ResetStatisticsForNewVideo()
        {
            _sessionStartTime = DateTime.Now;
            _userActions.Clear();
            _brainBitData.Clear();
            LogAction("new_video_load", TimeSpan.Zero, "Загрузка нового видео");
        }
        
        public void SendReportToHelperPanel(string neuralNetworkReport)
{
    // Получаем ссылку на MainWindow
    var mainWindow = Application.Current.MainWindow as MainWindow;
    if (mainWindow == null || mainWindow.HelperPanelComponent == null)
    {
        MessageBox.Show("HelperPanel не найден", "Ошибка", 
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }
    
    // Отправляем отчет в HelperPanel
    mainWindow.HelperPanelComponent.SetNeuralNetworkReport(neuralNetworkReport);
    
    // Показываем уведомление
    MessageBox.Show("Отчет нейронной сети отправлен в панель аналитики!", 
        "Отчет готов", MessageBoxButton.OK, MessageBoxImage.Information);
}
        
        private void StartVideoAfterBrainBitReady()
        {
            if (_mediaPlayer == null) return;

            try
            {
                // Проверяем, что видео загружено
                if (_mediaPlayer.Source == null && !string.IsNullOrEmpty(_currentVideoPath))
                {
                    _mediaPlayer.Source = new Uri(_currentVideoPath, UriKind.Absolute);
                }
        
                _mediaPlayer.Play();
                _isPlaying = true;
                _isWaitingForBrainBitData = false;
                UpdatePlayPauseButton();
        
                if (_progressTimer != null && !_progressTimer.IsEnabled)
                {
                    _progressTimer.Start();
                }
        
                // Логируем начало воспроизведения
                LogAction("video_start_after_brainbit", TimeSpan.Zero, 
                    new { BrainBitSamples = _brainBitData.Count, FirstAttention = _firstBrainBitData?.InstAttention ?? 0 });
        
                // Подписываемся на окончание видео для автоматической остановки анализа
                _mediaPlayer.MediaEnded += OnMediaEndedDuringAnalysis;
        
                MessageBox.Show($"Данные BrainBit получены! Начинаем воспроизведение.\n" +
                                $"Первое значение внимания: {_firstBrainBitData?.InstAttention:F2}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске видео: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateVideoInfo(string videoPath)
        {
            try
            {
                var fileInfo = new FileInfo(videoPath);

                VideoTitle.Text = Path.GetFileNameWithoutExtension(videoPath);
                VideoFormat.Text = Path.GetExtension(videoPath).ToUpper().TrimStart('.');

                double sizeInMB = fileInfo.Length / (1024.0 * 1024.0);
                VideoSize.Text = $"{sizeInMB:F1} MB";

                VideoQuality.Text = "Определение...";
                VideoDuration.Text = "Загрузка...";
            }
            catch
            {
                VideoTitle.Text = "Неизвестное видео";
                VideoFormat.Text = "N/A";
                VideoSize.Text = "N/A";
                VideoQuality.Text = "N/A";
                VideoDuration.Text = "N/A";
            }
        }

        private string GetVideoQuality(int width, int height)
        {
            if (width >= 3840 || height >= 2160)
                return "4K";
            else if (width >= 2560 || height >= 1440)
                return "2K/QHD";
            else if (width >= 1920 || height >= 1080)
                return "Full HD";
            else if (width >= 1280 || height >= 720)
                return "HD";
            else if (width >= 854 || height >= 480)
                return "SD";
            else if (width >= 640 || height >= 360)
                return "360p";
            else if (width >= 426 || height >= 240)
                return "240p";
            else if (width >= 256 || height >= 144)
                return "144p";
            else
                return $"{width}x{height}";
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer?.NaturalDuration.HasTimeSpan == true)
            {
                var duration = _mediaPlayer.NaturalDuration.TimeSpan;
                VideoDuration.Text = duration.ToString(@"hh\:mm\:ss");

                int width = (int)_mediaPlayer.NaturalVideoWidth;
                int height = (int)_mediaPlayer.NaturalVideoHeight;

                VideoQuality.Text = GetVideoQuality(width, height);

                UpdateVideoTimestamp();
            }
        }

        private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null) return;

            string actionType;
            if (_isPlaying)
            {
                _mediaPlayer.Pause();
                _progressTimer?.Stop();
                actionType = "pause";
            }
            else
            {
                if (_mediaPlayer.Source == null && !string.IsNullOrEmpty(_currentVideoPath))
                {
                    LoadVideo(_currentVideoPath);
                    actionType = "play_from_stop";
                }
                else
                {
                    _mediaPlayer.Play();
                    _progressTimer?.Start();
                    actionType = "play";
                }
            }

            // Логируем действие
            if (_mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                LogAction(actionType, _mediaPlayer.Position);
            }

            _isPlaying = !_isPlaying;
            UpdatePlayPauseButton();
        }

        private void UpdatePlayPauseButton()
        {
            PlayPauseButton.Content = _isPlaying ? "⏸" : "▶";
        }

        private void OnRewindClicked(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer?.NaturalDuration.HasTimeSpan == true)
            {
                var oldPosition = _mediaPlayer.Position;
                var newPosition = _mediaPlayer.Position - TimeSpan.FromSeconds(5);
                _mediaPlayer.Position = newPosition > TimeSpan.Zero ? newPosition : TimeSpan.Zero;
                UpdateVideoTimestamp();

                // Логируем перемотку назад
                LogAction("rewind_5s", oldPosition,
                    new { OldPosition = oldPosition, NewPosition = _mediaPlayer.Position });
            }
        }

        private void OnForwardClicked(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer?.NaturalDuration.HasTimeSpan == true)
            {
                var oldPosition = _mediaPlayer.Position;
                var newPosition = _mediaPlayer.Position + TimeSpan.FromSeconds(5);
                var maxDuration = _mediaPlayer.NaturalDuration.TimeSpan;
                _mediaPlayer.Position = newPosition < maxDuration ? newPosition : maxDuration;
                UpdateVideoTimestamp();

                // Логируем перемотку вперед
                LogAction("forward_5s", oldPosition,
                    new { OldPosition = oldPosition, NewPosition = _mediaPlayer.Position });
            }
        }

        private void OnFullscreenClicked(object sender, RoutedEventArgs e)
        {
            if (_isFullscreen)
            {
                ExitFullscreenMode();
                LogAction("exit_fullscreen", _mediaPlayer?.Position ?? TimeSpan.Zero);
            }
            else
            {
                EnterFullscreenMode();
                LogAction("enter_fullscreen", _mediaPlayer?.Position ?? TimeSpan.Zero);
            }
        }

        private void OnMediaEndedDuringAnalysis(object sender, RoutedEventArgs e)
        {
            // Останавливаем анализ при окончании видео
            _brainBitManager?.StopAnalysis();
    
            // Отписываемся от события
            if (_mediaPlayer != null)
            {
                _mediaPlayer.MediaEnded -= OnMediaEndedDuringAnalysis;
            }
    
            // Показываем результаты
            ShowFinalStatisticsWindow();
        }

private void EnterFullscreenMode()
{
    if (_mediaPlayer == null) return;

    // Сохраняем текущее состояние
    var originalPosition = _mediaPlayer.Position;
    var wasPlaying = _isPlaying;
    
    // Останавливаем оригинальный плеер ПЕРЕД созданием полноэкранного
    if (wasPlaying)
    {
        _mediaPlayer.Pause();
        _progressTimer?.Stop();
    }

    _fullscreenWindow = new Window
    {
        WindowStyle = WindowStyle.None,
        WindowState = WindowState.Maximized,
        Title = "Видео - полноэкранный режим",
        Background = Brushes.Black,
        Topmost = true
    };

    var fullscreenPlayer = new MediaElement
    {
        Source = _mediaPlayer.Source,
        Position = originalPosition,
        Stretch = Stretch.Uniform,
        LoadedBehavior = MediaState.Manual,
        UnloadedBehavior = MediaState.Manual,
        Volume = _mediaPlayer.Volume,
        IsMuted = _mediaPlayer.IsMuted,
        ScrubbingEnabled = true
    };

    // Воспроизводим в полноэкранном режиме, если оригинальный плеер был активен
    if (wasPlaying)
    {
        fullscreenPlayer.Play();
    }
    else
    {
        fullscreenPlayer.Pause();
    }

    var exitButton = new Button
    {
        Content = "✕",
        Width = 40,
        Height = 40,
        FontSize = 20,
        Background = Brushes.Transparent,
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 10, 10, 0),
        Cursor = Cursors.Hand
    };

    exitButton.Click += (s, e) => ExitFullscreenMode();

    var grid = new Grid();
    grid.Children.Add(fullscreenPlayer);
    grid.Children.Add(exitButton);

    _fullscreenWindow.Content = grid;

    // Обработчик закрытия окна
    _fullscreenWindow.Closed += (s, e) =>
    {
        if (!_isFullscreen) return;

        // Обновляем позицию в оригинальном плеере
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Position = fullscreenPlayer.Position;
            
            // ПРОВЕРЯЕМ ДЛИТЕЛЬНОСТЬ ПЕРЕД ОБРАЩЕНИЕМ К TimeSpan
            bool isPlayingInFullscreen = false;
            if (fullscreenPlayer.NaturalDuration.HasTimeSpan)
            {
                isPlayingInFullscreen = fullscreenPlayer.Position < fullscreenPlayer.NaturalDuration.TimeSpan && 
                                       fullscreenPlayer.Position > TimeSpan.Zero;
            }
            else
            {
                isPlayingInFullscreen = fullscreenPlayer.Position > TimeSpan.Zero;
            }
            
            if (isPlayingInFullscreen && fullscreenPlayer.HasAudio && fullscreenPlayer.CanPause)
            {
                _mediaPlayer.Play();
                _isPlaying = true;
                if (_progressTimer != null && !_progressTimer.IsEnabled)
                {
                    _progressTimer.Start();
                }
            }
            else
            {
                _mediaPlayer.Pause();
                _isPlaying = false;
            }
        }
        
        // Останавливаем полноэкранный плеер
        fullscreenPlayer.Stop();
        
        // Очищаем ресурсы
        fullscreenPlayer.Source = null;
        
        ExitFullscreenMode();
    };

    // Обработчик нажатия ESC
    _fullscreenWindow.PreviewKeyDown += (s, e) =>
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            ExitFullscreenMode();
        }
    };

    _isFullscreen = true;
    FullscreenButton.Content = "⛶";

    // Скрываем оригинальный плеер
    this.Visibility = Visibility.Collapsed;

    _fullscreenWindow.Show();
}

private void ExitFullscreenMode()
{
    if (!_isFullscreen || _fullscreenWindow == null || _mediaPlayer == null) return;

    // Получаем ссылку на полноэкранный плеер
    MediaElement? fullscreenPlayer = null;
    if (_fullscreenWindow.Content is Grid grid && grid.Children[0] is MediaElement player)
    {
        fullscreenPlayer = player;
        
        // Обновляем позицию в оригинальном плеере
        _mediaPlayer.Position = fullscreenPlayer.Position;
        
        // ПРОВЕРЯЕМ, ЧТО ДЛИТЕЛЬНОСТЬ ОПРЕДЕЛЕНА
        bool wasPlayingInFullscreen = false;
        if (fullscreenPlayer.NaturalDuration.HasTimeSpan)
        {
            wasPlayingInFullscreen = fullscreenPlayer.Position < fullscreenPlayer.NaturalDuration.TimeSpan && 
                                    fullscreenPlayer.Position > TimeSpan.Zero;
        }
        else
        {
            // Если длительность не определена, используем текущую позицию как индикатор
            wasPlayingInFullscreen = fullscreenPlayer.Position > TimeSpan.Zero;
        }
        
        // Если видео было воспроизведено, продолжаем в оригинальном плеере
        if (wasPlayingInFullscreen && fullscreenPlayer.HasAudio && fullscreenPlayer.CanPause)
        {
            _mediaPlayer.Play();
            _isPlaying = true;
            if (_progressTimer != null && !_progressTimer.IsEnabled)
            {
                _progressTimer.Start();
            }
        }
        else
        {
            _mediaPlayer.Pause();
            _isPlaying = false;
        }
        
        // Останавливаем полноэкранный плеер
        fullscreenPlayer.Stop();
        fullscreenPlayer.Source = null;
    }

    // Закрываем окно
    _fullscreenWindow.Close();
    _fullscreenWindow = null;

    _isFullscreen = false;
    FullscreenButton.Content = "⛶";

    // Показываем оригинальный плеер
    this.Visibility = Visibility.Visible;

    // Обновляем кнопку воспроизведения
    UpdatePlayPauseButton();
    
    // Обновляем временную метку
    UpdateVideoTimestamp();
}

private async void OnStatisticsClicked(object sender, RoutedEventArgs e)
{
    // 1. Перематываем видео в начало
    if (_mediaPlayer != null)
    {
        _mediaPlayer.Position = TimeSpan.Zero;
        if (_isPlaying)
        {
            _mediaPlayer.Pause();
            _isPlaying = false;
            UpdatePlayPauseButton();
        }
        
        UpdateVideoTimestamp();
    }
    
    // 2. Запускаем анализ BrainBit
    if (_mediaPlayer?.NaturalDuration.HasTimeSpan == true && _brainBitManager != null)
    {
        try
        {
            var videoDuration = _mediaPlayer.NaturalDuration.TimeSpan;
            
            // Очищаем предыдущие данные
            _brainBitData.Clear();
            _firstBrainBitData = null;
            _isWaitingForBrainBitData = true;
            
            // Показываем сообщение о начале анализа
            MessageBox.Show(
                "Начинается анализ BrainBit.\n" +
                "1. Убедитесь, что нейрогарнитура надета правильно\n" +
                "2. Сохраняйте спокойное положение в течение 20 секунд для калибровки\n" +
                "3. Видео запустится автоматически после получения данных",
                "Подготовка к анализу",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            
            // Запускаем анализ
            bool analysisStarted = await _brainBitManager.StartAnalysis(videoDuration);
            
            if (analysisStarted)
            {
                // Запускаем таймер для проверки данных
                StartBrainBitDataCheckTimer();
                
                MessageBox.Show(
                    "Анализ BrainBit запущен.\n" +
                    "Калибровка займет 20 секунд.\n" +
                    "Видео запустится автоматически при получении первых данных.",
                    "Анализ начат",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Не удалось запустить анализ BrainBit",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка запуска анализа: {ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    else
    {
        MessageBox.Show("Видео не загружено или не удалось определить длительность",
            "Ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}

private DispatcherTimer? _brainBitCheckTimer;

private void StartBrainBitDataCheckTimer()
{
    if (_brainBitCheckTimer != null)
    {
        _brainBitCheckTimer.Stop();
        _brainBitCheckTimer = null;
    }
    
    _brainBitCheckTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromSeconds(1)
    };
    
    int checkCounter = 0;
    const int maxWaitTime = 40; // Максимальное время ожидания 40 секунд
    
    _brainBitCheckTimer.Tick += (s, e) =>
    {
        checkCounter++;
        
        // Если ждем данные и они пришли - запускаем видео
        if (_isWaitingForBrainBitData && _brainBitData.Count > 0)
        {
            // Проверяем первое значение на валидность
            var firstData = _brainBitData.FirstOrDefault();
            if (firstData != null && IsBrainBitDataValid(firstData))
            {
                _brainBitCheckTimer?.Stop();
                Console.WriteLine($"Таймер: Найдены валидные данные BrainBit, запускаем видео");
                StartVideoAfterBrainBitReady();
            }
        }
        
        // Если слишком долго ждем
        if (checkCounter > maxWaitTime && _isWaitingForBrainBitData)
        {
            _brainBitCheckTimer?.Stop();
            MessageBox.Show(
                "Не удалось получить данные от BrainBit в течение 40 секунд.\n" +
                "Проверьте подключение нейрогарнитуры.",
                "Таймаут",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        
        // Информация о ожидании каждые 5 секунд
        if (checkCounter % 5 == 0 && _isWaitingForBrainBitData)
        {
            Console.WriteLine($"Ожидание данных BrainBit: {checkCounter} секунд, получено данных: {_brainBitData.Count}");
        }
    };
    
    _brainBitCheckTimer.Start();
    Console.WriteLine("Таймер проверки данных BrainBit запущен");
}
        private void ShowRealTimeAnalysisWindow(TimeSpan videoDuration)
        {
            var analysisWindow = new Window
            {
                Title = "Анализ эмоционального состояния",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var grid = new Grid();

            // Панель управления
            var controlPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var stopButton = new Button
            {
                Content = "⏹ Стоп анализ",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Background = Brushes.Red,
                Foreground = Brushes.White
            };

            var saveButton = new Button
            {
                Content = "💾 Сохранить данные",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5)
            };

            var exportButton = new Button
            {
                Content = "📤 Экспорт для нейронки",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5)
            };

            stopButton.Click += (s, e) =>
            {
                _brainBitManager?.StopAnalysis();
                analysisWindow.Close();
                ShowFinalStatisticsWindow();
            };

            saveButton.Click += (s, e) => SaveBrainBitData();
            exportButton.Click += (s, e) => ExportBrainBitData();

            controlPanel.Children.Add(stopButton);
            controlPanel.Children.Add(saveButton);
            controlPanel.Children.Add(exportButton);

            // Поле для вывода данных в реальном времени
            var textBox = new TextBox
            {
                Margin = new Thickness(10),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap
            };

            // Таймер для обновления данных в реальном времени
            var updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            updateTimer.Tick += (s, e) => UpdateRealTimeAnalysisTextBox(textBox);
            updateTimer.Start();

            // При закрытии окна останавливаем анализ
            analysisWindow.Closed += (s, e) =>
            {
                updateTimer.Stop();
                _brainBitManager?.StopAnalysis();
            };

            // Разметка
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(controlPanel, 0);
            Grid.SetRow(textBox, 1);

            grid.Children.Add(controlPanel);
            grid.Children.Add(textBox);

            analysisWindow.Content = grid;
            analysisWindow.Show();
        }

        private void UpdateRealTimeAnalysisTextBox(TextBox textBox)
        {
            if (_brainBitData.Count == 0)
            {
                textBox.Text =
                    "Ожидание данных от нейрогарнитуры...\nПожалуйста, подождите 20-30 секунд для калибровки.";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== ДАННЫЕ АНАЛИЗА В РЕАЛЬНОМ ВРЕМЕНИ ===\n");
            sb.AppendLine($"Всего получено измерений: {_brainBitData.Count}");
            sb.AppendLine($"Последнее обновление: {DateTime.Now:HH:mm:ss}\n");

            // Последние 5 измерений
            var recentData = _brainBitData.TakeLast(5).ToList();

            for (int i = 0; i < recentData.Count; i++)
            {
                var data = recentData[i];
                sb.AppendLine($"Измерение #{_brainBitData.Count - recentData.Count + i + 1}:");
                sb.AppendLine($"  Время видео: {data.VideoTime:mm\\:ss}");
                sb.AppendLine($"  Внимание: {data.InstAttention:F2} (относительное: {data.RelAttention:F2})");
                sb.AppendLine($"  Расслабление: {data.InstRelaxation:F2} (относительное: {data.RelRelaxation:F2})");
                sb.AppendLine($"  Спектральные волны:");
                sb.AppendLine($"    Альфа (α): {data.Alpha:F2}%");
                sb.AppendLine($"    Бета (β): {data.Beta:F2}%");
                sb.AppendLine($"    Гамма (γ): {data.Gamma:F2}%");
                sb.AppendLine($"    Тета (θ): {data.Theta:F2}%");
                sb.AppendLine($"    Дельта (δ): {data.Delta:F2}%");
                sb.AppendLine();
            }

            // Средние значения
            if (_brainBitData.Count >= 3)
            {
                sb.AppendLine("=== СРЕДНИЕ ЗНАЧЕНИЯ ===\n");
                sb.AppendLine($"Среднее внимание: {_brainBitData.Average(d => d.InstAttention):F2}");
                sb.AppendLine($"Среднее расслабление: {_brainBitData.Average(d => d.InstRelaxation):F2}");
                sb.AppendLine($"Максимальное внимание: {_brainBitData.Max(d => d.InstAttention):F2}");
                sb.AppendLine($"Минимальное внимание: {_brainBitData.Min(d => d.InstAttention):F2}");
            }

            textBox.Text = sb.ToString();
        }

private void ShowFinalStatisticsWindow()
{
    var statsWindow = new Window
    {
        Title = "Результаты анализа эмоций",
        Width = 800,
        Height = 600,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = Window.GetWindow(this),
        ShowInTaskbar = false,
        ResizeMode = ResizeMode.CanResize,
        MinWidth = 700,
        MinHeight = 500
    };

    var tabControl = new TabControl
    {
        Margin = new Thickness(5)
    };

    // Вкладка 1: Данные BrainBit
    var brainBitTab = new TabItem
    {
        Header = "📊 Данные нейроинтерфейса"
    };

    var brainBitTextBox = new TextBox
    {
        Margin = new Thickness(10),
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        IsReadOnly = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.NoWrap
    };

    UpdateBrainBitStatisticsTextBox(brainBitTextBox);
    brainBitTab.Content = brainBitTextBox;

    // Вкладка 2: Действия пользователя
    var actionsTab = new TabItem
    {
        Header = "🎬 Действия пользователя"
    };

    var actionsTextBox = new TextBox
    {
        Margin = new Thickness(10),
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        IsReadOnly = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.NoWrap
    };

    UpdateUserActionsTextBox(actionsTextBox);
    actionsTab.Content = actionsTextBox;

    // Вкладка 3: Комбинированные данные для нейронки
    var combinedTab = new TabItem
    {
        Header = "🧠 Данные для нейронной сети"
    };

    var combinedTextBox = new TextBox
    {
        Margin = new Thickness(10),
        FontFamily = new FontFamily("Consolas"),
        FontSize = 11,
        IsReadOnly = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.NoWrap
    };

    combinedTextBox.Text = GetCombinedDataForNeuralNetwork();
    combinedTab.Content = combinedTextBox;

    // Вкладка 4: Анкета пользователя (НОВАЯ)
    var userInfoTab = new TabItem
    {
        Header = "👤 Анкета пользователя"
    };

    var userInfoPanel = CreateUserQuestionnairePanel();
    userInfoTab.Content = userInfoPanel;

    // Панель кнопок
    var buttonPanel = new StackPanel
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 10, 0, 10)
    };

    var copyButton = new Button
    {
        Content = "📋 Копировать данные",
        Margin = new Thickness(5),
        Padding = new Thickness(10, 5, 10, 5),
        Width = 150,
        Height = 35,
        Cursor = Cursors.Hand,
        Background = new SolidColorBrush(Color.FromRgb(30, 144, 255)),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0)
    };

    var exportButton = new Button
    {
        Content = "💾 Экспорт в JSON",
        Margin = new Thickness(5),
        Padding = new Thickness(10, 5, 10, 5), // Left=10, Top=5, Right=10, Bottom=5
        Width = 150,
        Height = 35,
        Cursor = Cursors.Hand,
        Background = new SolidColorBrush(Color.FromRgb(30, 144, 255)),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0)
    };

    var closeButton = new Button
    {
        Content = "✕ Закрыть",
        Margin = new Thickness(5),
        Padding = new Thickness(10, 5, 10, 5),
        Width = 100,
        Height = 35,
        Cursor = Cursors.Hand,
        Background = new SolidColorBrush(Color.FromRgb(30, 144, 255)),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0)
    };

    copyButton.Click += (s, e) => Clipboard.SetText(GetCombinedDataForNeuralNetwork());
    exportButton.Click += (s, e) => ExportAllDataWithQuestionnaire(userInfoPanel);
    closeButton.Click += (s, e) => statsWindow.Close();

    buttonPanel.Children.Add(copyButton);
    buttonPanel.Children.Add(exportButton);
    buttonPanel.Children.Add(closeButton);

    // Добавляем все вкладки
    tabControl.Items.Add(brainBitTab);
    tabControl.Items.Add(actionsTab);
    tabControl.Items.Add(combinedTab);
    tabControl.Items.Add(userInfoTab);

    // Основной layout
    var mainGrid = new Grid();
    mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

    Grid.SetRow(tabControl, 0);
    Grid.SetRow(buttonPanel, 1);

    mainGrid.Children.Add(tabControl);
    mainGrid.Children.Add(buttonPanel);

    statsWindow.Content = mainGrid;
    statsWindow.Show();
}

private StackPanel CreateUserQuestionnairePanel()
{
    var stackPanel = new StackPanel
    {
        Margin = new Thickness(20)
    };

    // Создаем словарь для хранения элементов
    var elements = new Dictionary<string, FrameworkElement>();

    // Заголовок анкеты
    var headerText = new TextBlock
    {
        Text = "Анкета пользователя",
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        Foreground = Brushes.Navy,
        Margin = new Thickness(0, 0, 0, 20)
    };

    // Имя
    var nameLabel = new TextBlock
    {
        Text = "Имя:",
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 10, 0, 0)
    };

    var nameTextBox = new TextBox
    {
        Height = 30,
        FontSize = 14,
        Margin = new Thickness(0, 5, 0, 10),
        Tag = "name"
    };
    elements["name"] = nameTextBox;

    // Возраст
    var ageLabel = new TextBlock
    {
        Text = "Возраст:",
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 5, 0, 0)
    };

    var ageTextBox = new TextBox
    {
        Height = 30,
        FontSize = 14,
        Margin = new Thickness(0, 5, 0, 10),
        Tag = "age"
    };
    elements["age"] = ageTextBox;

    // Пол
    var genderLabel = new TextBlock
    {
        Text = "Пол:",
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 5, 0, 0)
    };

    var genderComboBox = new ComboBox
    {
        Height = 30,
        FontSize = 14,
        Margin = new Thickness(0, 5, 0, 10),
        Tag = "gender"
    };
    genderComboBox.Items.Add("Мужской");
    genderComboBox.Items.Add("Женский");
    genderComboBox.Items.Add("Предпочитаю не указывать");
    genderComboBox.SelectedIndex = 0;
    elements["gender"] = genderComboBox;

    // Образование
    var educationLabel = new TextBlock
    {
        Text = "Образование:",
        FontSize = 14,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 5, 0, 0)
    };

    var educationComboBox = new ComboBox
    {
        Height = 30,
        FontSize = 14,
        Margin = new Thickness(0, 5, 0, 10),
        Tag = "education"
    };
    educationComboBox.Items.Add("Среднее");
    educationComboBox.Items.Add("Среднее специальное");
    educationComboBox.Items.Add("Неоконченное высшее");
    educationComboBox.Items.Add("Высшее");
    educationComboBox.Items.Add("Ученая степень");
    educationComboBox.SelectedIndex = 3;
    elements["education"] = educationComboBox;

    // Добавляем все элементы в стекпанель в правильном порядке
    stackPanel.Children.Add(headerText);
    stackPanel.Children.Add(nameLabel);
    stackPanel.Children.Add(nameTextBox);
    stackPanel.Children.Add(ageLabel);
    stackPanel.Children.Add(ageTextBox);
    stackPanel.Children.Add(genderLabel);
    stackPanel.Children.Add(genderComboBox);
    stackPanel.Children.Add(educationLabel);
    stackPanel.Children.Add(educationComboBox);

    // Сохраняем ссылку на элементы в Tag
    stackPanel.Tag = elements;

    return stackPanel;
}

private Dictionary<string, object> GetQuestionnaireData(StackPanel questionnairePanel)
{
    var data = new Dictionary<string, object>();
    
    if (questionnairePanel?.Tag is Dictionary<string, FrameworkElement> elements)
    {
        foreach (var element in elements)
        {
            string value = string.Empty;
            
            switch (element.Value)
            {
                case TextBox textBox:
                    value = textBox.Text?.Trim() ?? string.Empty;
                    break;
                    
                case ComboBox comboBox:
                    value = comboBox.SelectedItem?.ToString() ?? string.Empty;
                    break;
                    
                case Slider slider:
                    value = slider.Value.ToString("F0");
                    break;
            }
            
            data[element.Key] = value;
        }
        
        // Добавляем дату заполнения
        data["questionnaire_date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data["questionnaire_timestamp"] = DateTime.Now.Ticks;
    }
    
    return data;
}

private string GetAllDataWithQuestionnaireJson(StackPanel questionnairePanel)
{
    var questionnaireData = GetQuestionnaireData(questionnairePanel);
    
    // Создаем полный объект данных
    var allData = new
    {
        Metadata = new
        {
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Application = "EmotionAnalyzer",
            Version = "1.0.0"
        },
        
        VideoInfo = new
        {
            FileName = Path.GetFileName(_currentVideoPath),
            FilePath = _currentVideoPath,
            Duration = _mediaPlayer?.NaturalDuration.TimeSpan.TotalSeconds ?? 0,
            Format = VideoFormat.Text,
            Quality = VideoQuality.Text,
            Size = VideoSize.Text,
            Title = VideoTitle.Text
        },
        
        UserQuestionnaire = questionnaireData,
        
        BrainBitData = new
        {
            TotalSamples = _brainBitData.Count,
            Duration = _brainBitData.Count > 0 ? 
                (_brainBitData.Last().Timestamp - _brainBitData.First().Timestamp).TotalSeconds : 0,
            Samples = _brainBitData.Select(d => new
            {
                d.Timestamp,
                VideoTimeSeconds = d.VideoTime.TotalSeconds,
                d.InstAttention,
                d.InstRelaxation,
                d.RelAttention,
                d.RelRelaxation,
                d.Alpha,
                d.Beta,
                d.Gamma,
                d.Theta,
                d.Delta
            }).ToList(),
            
            Statistics = new
            {
                AverageAttention = _brainBitData.Count > 0 ? _brainBitData.Average(d => d.InstAttention) : 0,
                AverageRelaxation = _brainBitData.Count > 0 ? _brainBitData.Average(d => d.InstRelaxation) : 0,
                MaxAttention = _brainBitData.Count > 0 ? _brainBitData.Max(d => d.InstAttention) : 0,
                MinAttention = _brainBitData.Count > 0 ? _brainBitData.Min(d => d.InstAttention) : 0,
                AttentionStdDev = _brainBitData.Count > 0 ? 
                    Math.Sqrt(_brainBitData.Average(d => Math.Pow(d.InstAttention - _brainBitData.Average(x => x.InstAttention), 2))) : 0
            }
        },
        
        UserActions = new
        {
            TotalActions = _userActions.Count,
            Actions = _userActions.Select(a => new
            {
                a.Timestamp,
                a.ActionType,
                VideoPositionSeconds = a.VideoPosition.TotalSeconds,
                AdditionalData = a.AdditionalData?.ToString()
            }).ToList(),
            
            Statistics = new
            {
                PlayCount = _userActions.Count(a => a.ActionType.Contains("play")),
                PauseCount = _userActions.Count(a => a.ActionType.Contains("pause")),
                RewindCount = _userActions.Count(a => a.ActionType.Contains("rewind")),
                ForwardCount = _userActions.Count(a => a.ActionType.Contains("forward")),
                SessionDuration = _userActions.Count > 0 ? 
                    (_userActions.Last().Timestamp - _userActions.First().Timestamp).TotalMinutes : 0
            }
        },
        
        SessionInfo = new
        {
            StartTime = _sessionStartTime,
            EndTime = DateTime.Now,
            DurationMinutes = (DateTime.Now - _sessionStartTime).TotalMinutes,
            VideoStartTime = _userActions.FirstOrDefault(a => a.ActionType.Contains("play"))?.Timestamp,
            VideoEndTime = _userActions.FirstOrDefault(a => a.ActionType.Contains("end"))?.Timestamp
        },
        
        RealTimeData = new
        {
            AttentionPoints = RealTimeDataManager.Instance.AttentionData.Count,
            RelaxationPoints = RealTimeDataManager.Instance.RelaxationData.Count,
            AlphaPoints = RealTimeDataManager.Instance.AlphaData.Count,
            BetaPoints = RealTimeDataManager.Instance.BetaData.Count
        }
    };

    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    return JsonSerializer.Serialize(allData, options);
}

private void ExportAllDataWithQuestionnaire(StackPanel questionnairePanel)
{
    try
    {
        var saveDialog = new SaveFileDialog
        {
            Filter = "JSON файлы (*.json)|*.json|Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
            FileName = $"emotion_analysis_full_{DateTime.Now:yyyyMMdd_HHmmss}",
            Title = "Экспорт всех данных с анкетой",
            DefaultExt = ".json",
            AddExtension = true
        };

        if (saveDialog.ShowDialog() == true)
        {
            // Проверяем заполнение обязательных полей
            var questionnaireData = GetQuestionnaireData(questionnairePanel);
            
            // Можно добавить проверку обязательных полей
            if (string.IsNullOrEmpty(questionnaireData["name"]?.ToString()) ||
                string.IsNullOrEmpty(questionnaireData["age"]?.ToString()))
            {
                var result = MessageBox.Show(
                    "Не все обязательные поля анкеты заполнены. Хотите продолжить экспорт?",
                    "Внимание",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            // Получаем все данные в JSON
            string jsonData = GetAllDataWithQuestionnaireJson(questionnairePanel);
            
            // Сохраняем в файл
            File.WriteAllText(saveDialog.FileName, jsonData);
            
            // Также создаем CSV версию для удобства
            string csvPath = Path.ChangeExtension(saveDialog.FileName, ".csv");
            SaveAllDataToCsv(csvPath, questionnaireData);
            
            // Показываем сообщение об успехе
            var message = $"Данные успешно экспортированы!\n\n" +
                         $"JSON файл: {saveDialog.FileName}\n" +
                         $"CSV файл: {csvPath}\n\n" +
                         $"Всего данных:\n" +
                         $"• BrainBit: {_brainBitData.Count} записей\n" +
                         $"• Действия: {_userActions.Count} записей\n" +
                         $"• Анкета: {questionnaireData.Count} полей";
            
            MessageBox.Show(message, 
                "Экспорт завершен", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
            
            // Также копируем JSON в буфер обмена
            Clipboard.SetText(jsonData);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ошибка при экспорте: {ex.Message}\n\n{ex.StackTrace}", 
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

private void SaveAllDataToCsv(string filePath, Dictionary<string, object> questionnaireData)
{
    try
    {
        var sb = new StringBuilder();
        
        // Заголовок CSV
        sb.AppendLine("=== ДАННЫЕ АНАЛИЗА ЭМОЦИЙ ===");
        sb.AppendLine($"Дата экспорта: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Видео: {Path.GetFileName(_currentVideoPath)}");
        sb.AppendLine($"Длительность сессии: {(DateTime.Now - _sessionStartTime).TotalMinutes:F1} мин");
        sb.AppendLine();
        
        // Раздел анкеты
        sb.AppendLine("=== АНКЕТА ПОЛЬЗОВАТЕЛЯ ===");
        foreach (var item in questionnaireData)
        {
            sb.AppendLine($"{item.Key}: {item.Value}");
        }
        sb.AppendLine();
        
        // Раздел статистики BrainBit
        if (_brainBitData.Count > 0)
        {
            sb.AppendLine("=== СТАТИСТИКА BRAINBIT ===");
            sb.AppendLine($"Всего записей: {_brainBitData.Count}");
            sb.AppendLine($"Период: {_brainBitData.First().Timestamp:HH:mm:ss} - {_brainBitData.Last().Timestamp:HH:mm:ss}");
            sb.AppendLine($"Среднее внимание: {_brainBitData.Average(d => d.InstAttention):F2}");
            sb.AppendLine($"Среднее расслабление: {_brainBitData.Average(d => d.InstRelaxation):F2}");
            sb.AppendLine($"Макс. внимание: {_brainBitData.Max(d => d.InstAttention):F2}");
            sb.AppendLine($"Мин. внимание: {_brainBitData.Min(d => d.InstAttention):F2}");
            sb.AppendLine();
            
            // Таблица с данными
            sb.AppendLine("=== ДАННЫЕ BRAINBIT (первые 10 записей) ===");
            sb.AppendLine("Время;Время видео;Внимание;Расслабление;Альфа;Бета;Гамма;Тета;Дельта");
            
            foreach (var data in _brainBitData.Take(10))
            {
                sb.AppendLine($"{data.Timestamp:HH:mm:ss};" +
                             $"{data.VideoTime:mm\\:ss};" +
                             $"{data.InstAttention:F2};" +
                             $"{data.InstRelaxation:F2};" +
                             $"{data.Alpha:F2};" +
                             $"{data.Beta:F2};" +
                             $"{data.Gamma:F2};" +
                             $"{data.Theta:F2};" +
                             $"{data.Delta:F2}");
            }
        }
        sb.AppendLine();
        
        // Раздел действий пользователя
        if (_userActions.Count > 0)
        {
            sb.AppendLine("=== ДЕЙСТВИЯ ПОЛЬЗОВАТЕЛЯ ===");
            sb.AppendLine($"Всего действий: {_userActions.Count}");
            sb.AppendLine($"Воспроизведение: {_userActions.Count(a => a.ActionType.Contains("play"))}");
            sb.AppendLine($"Пауза: {_userActions.Count(a => a.ActionType.Contains("pause"))}");
            sb.AppendLine($"Перемотка: {_userActions.Count(a => a.ActionType.Contains("rewind") || a.ActionType.Contains("forward"))}");
            sb.AppendLine();
            
            sb.AppendLine("=== ХРОНОЛОГИЯ ДЕЙСТВИЙ ===");
            sb.AppendLine("Время;Тип действия;Позиция видео");
            
            foreach (var action in _userActions)
            {
                sb.AppendLine($"{action.Timestamp:HH:mm:ss};{action.ActionType};{action.VideoPosition:mm\\:ss}");
            }
        }
        
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка сохранения CSV: {ex.Message}");
    }
}

        private void UpdateBrainBitStatisticsTextBox(TextBox textBox)
        {
            if (_brainBitData.Count == 0)
            {
                textBox.Text = "Данные нейроинтерфейса не собраны.\nЗапустите анализ для сбора данных.";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== ДАННЫЕ НЕЙРОИНТЕРФЕЙСА BRAINBIT ===\n");
            sb.AppendLine($"Всего измерений: {_brainBitData.Count}");
            sb.AppendLine($"Длительность анализа: {_brainBitData.Last().VideoTime:mm\\:ss}");
            sb.AppendLine(
                $"Диапазон времени: {_brainBitData.First().Timestamp:HH:mm:ss} - {_brainBitData.Last().Timestamp:HH:mm:ss}\n");

            // Статистика по вниманию и расслаблению
            sb.AppendLine("=== ЭМОЦИОНАЛЬНЫЕ ПОКАЗАТЕЛИ ===\n");
            sb.AppendLine($"Среднее внимание: {_brainBitData.Average(d => d.InstAttention):F2}");
            sb.AppendLine($"Среднее расслабление: {_brainBitData.Average(d => d.InstRelaxation):F2}");
            sb.AppendLine($"Максимальное внимание: {_brainBitData.Max(d => d.InstAttention):F2}");
            sb.AppendLine($"Минимальное внимание: {_brainBitData.Min(d => d.InstAttention):F2}");
            sb.AppendLine($"Максимальное расслабление: {_brainBitData.Max(d => d.InstRelaxation):F2}");
            sb.AppendLine($"Минимальное расслабление: {_brainBitData.Min(d => d.InstRelaxation):F2}\n");

            // Спектральные волны
            sb.AppendLine("=== СПЕКТРАЛЬНЫЕ ВОЛНЫ (средние значения) ===\n");
            sb.AppendLine($"Альфа (α): {_brainBitData.Average(d => d.Alpha):F2}% - Расслабление, медитация");
            sb.AppendLine($"Бета (β): {_brainBitData.Average(d => d.Beta):F2}% - Активное мышление, концентрация");
            sb.AppendLine($"Гамма (γ): {_brainBitData.Average(d => d.Gamma):F2}% - Высшая когнитивная деятельность");
            sb.AppendLine($"Тета (θ): {_brainBitData.Average(d => d.Theta):F2}% - Творчество, сновидения");
            sb.AppendLine($"Дельта (δ): {_brainBitData.Average(d => d.Delta):F2}% - Глубокий сон, восстановление\n");

            // Последние 10 измерений
            sb.AppendLine("=== ПОСЛЕДНИЕ ИЗМЕРЕНИЯ ===\n");
            var recentData = _brainBitData.TakeLast(10).ToList();

            for (int i = 0; i < recentData.Count; i++)
            {
                var data = recentData[i];
                sb.AppendLine(
                    $"{i + 1:00}. [{data.VideoTime:mm\\:ss}] Вн: {data.InstAttention:F2} | Рассл: {data.InstRelaxation:F2} | " +
                    $"α:{data.Alpha:F1}% β:{data.Beta:F1}%");
            }

            textBox.Text = sb.ToString();
        }

        private void UpdateUserActionsTextBox(TextBox textBox)
        {
            if (_userActions.Count == 0)
            {
                textBox.Text = "Действия пользователя не записаны.";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== ДЕЙСТВИЯ ПОЛЬЗОВАТЕЛЯ ===\n");
            sb.AppendLine($"Видео: {Path.GetFileName(_currentVideoPath) ?? "Не загружено"}");
            sb.AppendLine($"Начало сессии: {_sessionStartTime:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine($"Всего действий: {_userActions.Count}\n");

            // Группировка по типам действий
            var actionGroups = _userActions.GroupBy(a => a.ActionType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count);

            sb.AppendLine("=== СТАТИСТИКА ПО ТИПАМ ДЕЙСТВИЙ ===\n");
            foreach (var group in actionGroups)
            {
                sb.AppendLine($"  {group.Type}: {group.Count} раз");
            }

            sb.AppendLine("\n=== ХРОНОЛОГИЯ ДЕЙСТВИЙ ===\n");

            int index = 1;
            foreach (var action in _userActions)
            {
                sb.AppendLine($"{index:000}. {action}");
                index++;
            }

            textBox.Text = sb.ToString();
        }

        private string GetCombinedDataForNeuralNetwork()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"emotion_analysis_session\": {");
            sb.AppendLine($"    \"video\": \"{JsonEscape(Path.GetFileName(_currentVideoPath))}\",");
            sb.AppendLine($"    \"session_start\": \"{_sessionStartTime:yyyy-MM-ddTHH:mm:ss}\",");
            sb.AppendLine($"    \"user_actions_count\": {_userActions.Count},");
            sb.AppendLine($"    \"brainbit_samples_count\": {_brainBitData.Count},");

            // Данные действий пользователя
            sb.AppendLine("    \"user_actions\": [");
            for (int i = 0; i < _userActions.Count; i++)
            {
                var action = _userActions[i];
                sb.Append("      {");
                sb.Append($"\"time\": \"{action.Timestamp:HH:mm:ss}\", ");
                sb.Append($"\"type\": \"{JsonEscape(action.ActionType)}\", ");
                sb.Append($"\"video_pos\": {(int)action.VideoPosition.TotalSeconds}");
                if (action.AdditionalData != null)
                {
                    sb.Append($", \"data\": \"{JsonEscape(action.AdditionalData.ToString())}\"");
                }

                sb.Append("}");
                if (i < _userActions.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("    ],");

            // Данные BrainBit
            sb.AppendLine("    \"brainbit_data\": [");
            for (int i = 0; i < _brainBitData.Count; i++)
            {
                var data = _brainBitData[i];
                sb.Append("      {");
                sb.Append($"\"timestamp\": \"{data.Timestamp:HH:mm:ss}\", ");
                sb.Append($"\"video_time\": {(int)data.VideoTime.TotalSeconds}, ");
                sb.Append($"\"attention\": {data.InstAttention:F2}, ");
                sb.Append($"\"relaxation\": {data.InstRelaxation:F2}, ");
                sb.Append($"\"rel_attention\": {data.RelAttention:F2}, ");
                sb.Append($"\"rel_relaxation\": {data.RelRelaxation:F2}, ");
                sb.Append($"\"alpha\": {data.Alpha:F2}, ");
                sb.Append($"\"beta\": {data.Beta:F2}, ");
                sb.Append($"\"gamma\": {data.Gamma:F2}, ");
                sb.Append($"\"theta\": {data.Theta:F2}, ");
                sb.Append($"\"delta\": {data.Delta:F2}");
                sb.Append("}");
                if (i < _brainBitData.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("    ]");

            sb.AppendLine("  }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void ExportCombinedData()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON файлы (*.json)|*.json|Текстовые файлы (*.txt)|*.txt",
                    FileName = $"emotion_analysis_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Title = "Экспорт данных анализа"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string jsonData = GetCombinedDataForNeuralNetwork();
                    File.WriteAllText(saveDialog.FileName, jsonData);

                    MessageBox.Show($"Данные экспортированы в файл: {saveDialog.FileName}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveBrainBitData()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv|Текстовые файлы (*.txt)|*.txt",
                    FileName = $"brainbit_data_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Title = "Сохранение данных BrainBit"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    // Заголовок CSV
                    sb.AppendLine(
                        "Timestamp,VideoTime,Attention,Relaxation,RelAttention,RelRelaxation,Alpha,Beta,Gamma,Theta,Delta");

                    foreach (var data in _brainBitData)
                    {
                        sb.AppendLine(
                            $"{data.Timestamp:HH:mm:ss},{(int)data.VideoTime.TotalSeconds},{data.InstAttention:F2},{data.InstRelaxation:F2},{data.RelAttention:F2},{data.RelRelaxation:F2},{data.Alpha:F2},{data.Beta:F2},{data.Gamma:F2},{data.Theta:F2},{data.Delta:F2}");
                    }

                    File.WriteAllText(saveDialog.FileName, sb.ToString());

                    MessageBox.Show($"Данные сохранены в файл: {saveDialog.FileName}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportBrainBitData()
        {
            try
            {
                string jsonData = JsonSerializer.Serialize(_brainBitData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                Clipboard.SetText(jsonData);
                MessageBox.Show("Данные BrainBit скопированы в буфер обмена в формате JSON!",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обработчики событий BrainBit
        private void OnBrainBitDataReceived(BrainBitManager.AnalysisData data)
        {
            Dispatcher.Invoke(() => 
            { 
                _brainBitData.Add(data);
        
                // Сохраняем первое полученное значение
                if (_firstBrainBitData == null)
                {
                    _firstBrainBitData = data;
                    Console.WriteLine($"Первые данные BrainBit получены: Внимание={data.InstAttention:F2}, Альфа={data.Alpha:F1}%");
                }
        
                // Проверяем, что данные валидны
                if (IsBrainBitDataValid(data))
                {
                    Console.WriteLine($"Валидные данные BrainBit получены: Внимание={data.InstAttention:F2}");
            
                    // Если ждем данные для запуска видео - запускаем
                    if (_isWaitingForBrainBitData)
                    {
                        Console.WriteLine("Запускаем видео после получения данных BrainBit...");
                        StartVideoAfterBrainBitReady();
                    }
                }
            });
        }

        private void OnBrainBitStateChanged(bool isAnalyzing)
        {
            _isBrainBitAnalyzing = isAnalyzing;
        }

        private string JsonEscape(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            return input.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private void OnUploadVideoClicked(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Видео файлы (*.mp4;*.mov)|*.mp4;*.mov|Все файлы (*.*)|*.*",
                Multiselect = false,
                Title = "Выберите видео файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadVideo(openFileDialog.FileName);
            }
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            _isPlaying = false;
            UpdatePlayPauseButton();
            _progressTimer?.Stop();

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Position = TimeSpan.Zero;
                UpdateVideoTimestamp();
            }

            // Логируем окончание видео
            LogAction("video_end", _mediaPlayer?.Position ?? TimeSpan.Zero);
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show($"Не удалось загрузить видео: {e.ErrorException.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            _isPlaying = false;
            UpdatePlayPauseButton();
            _progressTimer?.Stop();

            // Логируем ошибку
            LogAction("video_error", TimeSpan.Zero, e.ErrorException.Message);
        }

        private void UpdateVideoProgress(object? sender, EventArgs e)
        {
            UpdateVideoTimestamp();
        }

        private void UpdateVideoTimestamp()
        {
            if (_mediaPlayer?.NaturalDuration.HasTimeSpan == true)
            {
                var currentTime = _mediaPlayer.Position;
                var totalTime = _mediaPlayer.NaturalDuration.TimeSpan;

                VideoTimestamp.Text = $"{currentTime:mm\\:ss} / {totalTime:mm\\:ss}";
            }
            else if (_mediaPlayer != null && _mediaPlayer.Source != null)
            {
                VideoTimestamp.Text = $"00:00 / --:--";
            }
        }

        public void StopVideo()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                _isPlaying = false;
                UpdatePlayPauseButton();
                _progressTimer?.Stop();

                // Логируем остановку видео
                LogAction("manual_stop", _mediaPlayer.Position);
            }

            if (_isFullscreen)
            {
                ExitFullscreenMode();
            }
        }

        public void Cleanup()
        {
            StopVideo();
            _progressTimer?.Stop();
    
            // Останавливаем BrainBit анализ
            _brainBitManager?.StopAnalysis();
            _brainBitManager?.Dispose();
    
            // Останавливаем таймер проверки
            _brainBitCheckTimer?.Stop();
            _brainBitCheckTimer = null;
    
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Source = null;
                _mediaPlayer.Close();
            }
    
            if (_fullscreenWindow != null)
            {
                _fullscreenWindow.Close();
                _fullscreenWindow = null;
            }
    
            // Автосохранение данных
            AutoSaveAnalysisData();
        }

        private void AutoSaveAnalysisData()
        {
            if (_brainBitData.Count > 0 || _userActions.Count > 0)
            {
                try
                {
                    string autoSavePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "EmotionAnalyzer",
                        "Analysis",
                        $"analysis_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                    );

                    Directory.CreateDirectory(Path.GetDirectoryName(autoSavePath)!);

                    // Сохраняем комбинированные данные (BrainBit + действия пользователя)
                    string jsonData = GetCombinedDataForNeuralNetwork();
                    File.WriteAllText(autoSavePath, jsonData);

                    // Также сохраняем отдельно CSV для BrainBit
                    if (_brainBitData.Count > 0)
                    {
                        string csvPath = Path.ChangeExtension(autoSavePath, ".csv");
                        SaveBrainBitDataToFile(csvPath);
                    }

                    Console.WriteLine($"Данные анализа сохранены: {autoSavePath}");

                    // Показать уведомление
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Данные анализа сохранены в:\n{autoSavePath}",
                            "Данные сохранены",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка автосохранения данных: {ex.Message}");
                }
            }
        }

// Вспомогательный метод для сохранения BrainBit данных в CSV
        private void SaveBrainBitDataToFile(string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                // Заголовок CSV
                sb.AppendLine(
                    "Timestamp,VideoTimeSec,Attention,Relaxation,RelAttention,RelRelaxation,Alpha,Beta,Gamma,Theta,Delta");

                foreach (var data in _brainBitData)
                {
                    sb.AppendLine(
                        $"{data.Timestamp:HH:mm:ss},{(int)data.VideoTime.TotalSeconds},{data.InstAttention:F2},{data.InstRelaxation:F2},{data.RelAttention:F2},{data.RelRelaxation:F2},{data.Alpha:F2},{data.Beta:F2},{data.Gamma:F2},{data.Theta:F2},{data.Delta:F2}");
                }

                File.WriteAllText(filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения CSV: {ex.Message}");
            }
        }

        public bool IsPlaying => _isPlaying;

        public void ForceExitFullscreen()
        {
            if (_isFullscreen)
            {
                ExitFullscreenMode();
            }
        }

        // Методы для получения данных
        public List<UserAction> GetUserActions()
        {
            return new List<UserAction>(_userActions);
        }

        public List<BrainBitManager.AnalysisData> GetBrainBitData()
        {
            return new List<BrainBitManager.AnalysisData>(_brainBitData);
        }

        public string GetAnalysisSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== СВОДКА АНАЛИЗА ===\n");
            sb.AppendLine($"Видео: {Path.GetFileName(_currentVideoPath)}");
            sb.AppendLine($"Действия пользователя: {_userActions.Count}");
            sb.AppendLine($"Измерения BrainBit: {_brainBitData.Count}");

            if (_brainBitData.Count > 0)
            {
                sb.AppendLine($"\nСредние показатели:");
                sb.AppendLine($"  Внимание: {_brainBitData.Average(d => d.InstAttention):F2}");
                sb.AppendLine($"  Расслабление: {_brainBitData.Average(d => d.InstRelaxation):F2}");
            }

            return sb.ToString();
        }
    }
}
