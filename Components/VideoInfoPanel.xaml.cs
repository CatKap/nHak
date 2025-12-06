using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Windows.Input;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace EmotionAnalyzer.Components
{
    public partial class VideoInfoPanel : UserControl
    {
        private MediaElement? _mediaPlayer;
        private DispatcherTimer? _progressTimer;
        private bool _isPlaying = false;
        private bool _isFullscreen = false;
        private string _currentVideoPath = string.Empty;
        private Window? _fullscreenWindow;

        // Статистика пользовательских действий
        private List<UserAction> _userActions = new List<UserAction>();
        private DateTime _sessionStartTime;
        private bool _isCollectingStats = true;

        // Для BrainBit анализа
        private BrainBitManager? _brainBitManager;
        private List<BrainBitManager.AnalysisData> _brainBitData = new List<BrainBitManager.AnalysisData>();
        private bool _isBrainBitAnalyzing = false;

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

// Метод для обновления графиков
        private void UpdateRealTimeCharts(BrainBitManager.AnalysisData data)
        {
            // Получаем ссылки на панели через родительское окно
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;

            // Обновляем EngagementPanel
            if (mainWindow.EngagementPanelComponent != null)
            {
                mainWindow.EngagementPanelComponent.UpdateEngagementData(
                    data.InstAttention,
                    data.RelAttention,
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

                // Сбрасываем статистику для нового видео
                ResetStatisticsForNewVideo();

                if (_isPlaying)
                {
                    _mediaPlayer.Stop();
                    _progressTimer?.Stop();
                    _isPlaying = false;
                }

                _mediaPlayer.Source = new Uri(videoPath, UriKind.Absolute);
                _mediaPlayer.Play();

                _isPlaying = true;
                UpdatePlayPauseButton();

                if (_progressTimer != null && !_progressTimer.IsEnabled)
                {
                    _progressTimer.Start();
                }

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

        private void ResetStatisticsForNewVideo()
        {
            _sessionStartTime = DateTime.Now;
            _userActions.Clear();
            _brainBitData.Clear();
            LogAction("new_video_load", TimeSpan.Zero, "Загрузка нового видео");
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
                Position = _mediaPlayer.Position,
                Stretch = Stretch.Uniform,
                LoadedBehavior = MediaState.Manual,
                Volume = _mediaPlayer.Volume,
                IsMuted = _mediaPlayer.IsMuted
            };

            if (_isPlaying)
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

            _fullscreenWindow.Closed += (s, e) =>
            {
                if (!_isFullscreen) return;

                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Position = fullscreenPlayer.Position;
                }

                ExitFullscreenMode();
            };

            _fullscreenWindow.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    ExitFullscreenMode();
                }
            };

            _isFullscreen = true;
            FullscreenButton.Content = "⛶";

            this.Visibility = Visibility.Collapsed;

            _fullscreenWindow.Show();
        }

        private void ExitFullscreenMode()
        {
            if (!_isFullscreen || _fullscreenWindow == null || _mediaPlayer == null) return;

            if (_fullscreenWindow.Content is Grid grid && grid.Children[0] is MediaElement fullscreenPlayer)
            {
                _mediaPlayer.Position = fullscreenPlayer.Position;

                if (fullscreenPlayer.HasAudio && fullscreenPlayer.CanPause)
                {
                    if (fullscreenPlayer.Position < fullscreenPlayer.NaturalDuration.TimeSpan)
                    {
                        _mediaPlayer.Play();
                        _isPlaying = true;
                        _progressTimer?.Start();
                    }
                }
            }

            _fullscreenWindow.Close();
            _fullscreenWindow = null;

            _isFullscreen = false;
            FullscreenButton.Content = "⛶";

            this.Visibility = Visibility.Visible;

            UpdatePlayPauseButton();
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

                    // Показываем окно ожидания
                    var waitWindow = new Window
                    {
                        Title = "Подготовка анализа",
                        Width = 300,
                        Height = 150,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = Window.GetWindow(this)
                    };

                    var stackPanel = new StackPanel
                    {
                        Margin = new Thickness(20),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var progressBar = new ProgressBar
                    {
                        IsIndeterminate = true,
                        Width = 200,
                        Height = 20,
                        Margin = new Thickness(0, 0, 0, 10)
                    };

                    var textBlock = new TextBlock
                    {
                        Text = "Подготовка нейроинтерфейса...\nЭто может занять до 30 секунд",
                        TextAlignment = TextAlignment.Center
                    };

                    stackPanel.Children.Add(progressBar);
                    stackPanel.Children.Add(textBlock);
                    waitWindow.Content = stackPanel;

                    // Запускаем окно в фоновом режиме
                    waitWindow.Show();

                    // Запускаем анализ
                    bool analysisStarted = await _brainBitManager.StartAnalysis(videoDuration);

                    // Закрываем окно ожидания
                    waitWindow.Close();

                    if (analysisStarted)
                    {
                        MessageBox.Show(
                            "Анализ начался. Пожалуйста, носите нейрогарнитуру во время просмотра видео.\nНажмите 'Стоп' в окне анализа для завершения.",
                            "Анализ запущен",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Показываем окно анализа в реальном времени
                        ShowRealTimeAnalysisWindow(videoDuration);
                    }
                    else
                    {
                        MessageBox.Show("Не удалось запустить анализ. Проверьте подключение нейрогарнитуры.",
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
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var tabControl = new TabControl();

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

            // Панель кнопок
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var copyButton = new Button
            {
                Content = "📋 Копировать данные",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5)
            };

            var exportButton = new Button
            {
                Content = "💾 Экспорт в JSON",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5)
            };

            copyButton.Click += (s, e) => Clipboard.SetText(GetCombinedDataForNeuralNetwork());
            exportButton.Click += (s, e) => ExportCombinedData();

            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(exportButton);

            // Основной layout
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            tabControl.Items.Add(brainBitTab);
            tabControl.Items.Add(actionsTab);
            tabControl.Items.Add(combinedTab);

            Grid.SetRow(tabControl, 0);
            Grid.SetRow(buttonPanel, 1);

            mainGrid.Children.Add(tabControl);
            mainGrid.Children.Add(buttonPanel);

            statsWindow.Content = mainGrid;
            statsWindow.ShowDialog();
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
            Dispatcher.Invoke(() => { _brainBitData.Add(data); });
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