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
        private CalibrationWindow? _calibrationWindow;
        // Статистика пользовательских действий
        private List<UserAction> _userActions = new List<UserAction>();
        private DateTime _sessionStartTime;
        private bool _isCollectingStats = true;
        
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
            
            // Обновляем UI статистики если открыто окно
            UpdateStatisticsUI();
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
                
                // Подписываемся на события перемотки
                _mediaPlayer.MouseDown += OnMediaPlayerMouseDown;
            }
        }

        private void OnMediaPlayerMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_mediaPlayer != null && e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                // Двойной клик по видео - перемотка вперед
                LogAction("double_click_forward", _mediaPlayer.Position, "Двойной клик");
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
                LogAction("rewind_5s", oldPosition, new { OldPosition = oldPosition, NewPosition = _mediaPlayer.Position });
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
                LogAction("forward_5s", oldPosition, new { OldPosition = oldPosition, NewPosition = _mediaPlayer.Position });
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

        private void OnStatisticsClicked(object sender, RoutedEventArgs e)
{
    ShowCalibrationWindow();
}

    private void ShowCalibrationWindow()
    {
        // Создаем окно калибровки на весь экран
        var calibrationWindow = new Window
        {
            Title = "Калибровка устройства",
            WindowState = WindowState.Maximized,
            WindowStyle = WindowStyle.None,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)), // #F8F9FA
            Topmost = true
        };

        // Основной контейнер
        var mainGrid = new Grid();
        mainGrid.Background = Brushes.Transparent;

        // Центральный блок с информацией (белая карточка)
        var centerCard = new Border
        {
            Width = 600,
            Padding = new Thickness(40),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)), // #E0E0E0
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var centerPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Brushes.Transparent
        };

        // Иконка устройства
        var deviceIcon = new Grid
        {
            Width = 100,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 30)
        };

        var iconEllipse = new Border
        {
            Width = 100,
            Height = 100,
            CornerRadius = new CornerRadius(50), // Делаем круг
            Background = new SolidColorBrush(Color.FromArgb(20, 30, 144, 255))
        };

        var iconText = new TextBlock
        {
            Text = "📷",
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        deviceIcon.Children.Add(iconEllipse);
        deviceIcon.Children.Add(iconText);

        // 1. Устройство подключено
        var deviceConnectedText = new TextBlock
        {
            Text = "Устройство подключено",
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 144, 255)), // #1E90FF
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15)
        };

        // 2. Начинается калибровка...
        var calibrationStartText = new TextBlock
        {
            Text = "Начинается калибровка...",
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)), // #333333
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 40)
        };

        // 3. Предупреждение в синей рамке
        var warningBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(15, 30, 144, 255)), // #1E90FF с прозрачностью
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 144, 255)), // #1E90FF
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(25, 20, 25, 20),
            Margin = new Thickness(0, 0, 0, 40),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var warningPanel = new StackPanel();
        
        var warningTitle = new TextBlock
        {
            Text = "⚠️ ВНИМАНИЕ",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 144, 255)), // #1E90FF
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var warningText = new TextBlock
        {
            Text = "Не двигайтесь и не думайте ни о чём",
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)), // #333333
            HorizontalAlignment = HorizontalAlignment.Center
        };

        warningPanel.Children.Add(warningTitle);
        warningPanel.Children.Add(warningText);
        warningBorder.Child = warningPanel;

        // 4. Таймер в отдельном блоке
        var timerBlock = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 40)
        };

        var timerText = new TextBlock
        {
            Text = "20",
            FontSize = 64,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 144, 255)), // #1E90FF
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var secondsText = new TextBlock
        {
            Text = "секунд",
            FontSize = 20,
            Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), // #666666
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };

        // Прогресс-бар
        var progressBarBorder = new Border
        {
            Width = 300,
            Height = 12,
            Background = new SolidColorBrush(Color.FromRgb(235, 235, 235)), // #EBEBEB
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 20, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var progressFill = new Border
        {
            Name = "ProgressFill",
            Width = 0,
            Height = 12,
            Background = new SolidColorBrush(Color.FromRgb(30, 144, 255)), // #1E90FF
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        progressBarBorder.Child = progressFill;

        timerBlock.Children.Add(timerText);
        timerBlock.Children.Add(secondsText);
        timerBlock.Children.Add(progressBarBorder);

        // Собираем центральную панель
        centerPanel.Children.Add(deviceIcon);
        centerPanel.Children.Add(deviceConnectedText);
        centerPanel.Children.Add(calibrationStartText);
        centerPanel.Children.Add(warningBorder);
        centerPanel.Children.Add(timerBlock);

        centerCard.Child = centerPanel;

        // Добавляем в главный Grid
        mainGrid.Children.Add(centerCard);

        // Кнопка отмены (стилизованная)
        var cancelButton = new Button
        {
            Content = "Отменить калибровку",
            Width = 180,
            Height = 40,
            FontSize = 14,
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)), // #CCCCCC
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), // #666666
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 50),
            Cursor = Cursors.Hand
        };

        // Стиль для кнопки отмены
        var cancelButtonStyle = new Style(typeof(Button));
        var controlTemplate = new ControlTemplate(typeof(Button));

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.PaddingProperty, new Thickness(15, 8, 15, 8));

        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

        border.AppendChild(contentPresenter);
        controlTemplate.VisualTree = border;

        // Триггеры для наведения и нажатия
        var isMouseOverTrigger = new Trigger
        {
            Property = Button.IsMouseOverProperty,
            Value = true,
            Setters = {
                new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(245, 245, 245))), // #F5F5F5
                new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(51, 51, 51))) // #333333
            }
        };

        var isPressedTrigger = new Trigger
        {
            Property = Button.IsPressedProperty,
            Value = true,
            Setters = {
                new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(224, 224, 224))) // #E0E0E0
            }
        };

        controlTemplate.Triggers.Add(isMouseOverTrigger);
        controlTemplate.Triggers.Add(isPressedTrigger);

        cancelButtonStyle.Setters.Add(new Setter(Button.TemplateProperty, controlTemplate));
        cancelButton.Style = cancelButtonStyle;

        cancelButton.Click += (s, args) =>
        {
            calibrationWindow.Close();
        };

        mainGrid.Children.Add(cancelButton);

        calibrationWindow.Content = mainGrid;

        // Таймер на 20 секунд
        int secondsRemaining = 20;
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        timer.Tick += (s, args) =>
        {
            secondsRemaining--;
            timerText.Text = secondsRemaining.ToString();
            
            // Обновляем прогресс-бар
            double progress = ((20 - secondsRemaining) / 20.0) * 300;
            progressFill.Width = progress;

            if (secondsRemaining <= 0)
            {
                timer.Stop();
                
                // Показываем сообщение о завершении в стиле приложения
                var completionDialog = new Window
                {
                    Title = "Калибровка завершена",
                    Width = 400,
                    Height = 220,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)) // #F8F9FA
                };

                var completionCard = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)), // #E0E0E0
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(30),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var completionPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Background = Brushes.Transparent
                };

                var checkIcon = new TextBlock
                {
                    Text = "✅",
                    FontSize = 48,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };

                var completionText = new TextBlock
                {
                    Text = "Калибровка завершена успешно!",
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(30, 144, 255)), // #1E90FF
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };

                var autoCloseText = new TextBlock
                {
                    Text = "Окно закроется автоматически...",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), // #888888
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                completionPanel.Children.Add(checkIcon);
                completionPanel.Children.Add(completionText);
                completionPanel.Children.Add(autoCloseText);
                completionCard.Child = completionPanel;
                completionDialog.Content = completionCard;

                // Автоматически закрываем через 2 секунды
                var closeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };

                closeTimer.Tick += (closeS, closeArgs) =>
                {
                    closeTimer.Stop();
                    completionDialog.Close();
                    calibrationWindow.Close();
                };

                closeTimer.Start();
                completionDialog.ShowDialog();
            }
        };

        // Обработчик клавиши Escape для выхода
        calibrationWindow.PreviewKeyDown += (s, args) =>
        {
            if (args.Key == Key.Escape)
            {
                calibrationWindow.Close();
            }
        };

        // Показываем окно и запускаем таймер
        calibrationWindow.Show();
        timer.Start();
    }
        private void ShowStatisticsWindow()
        {
            var statsWindow = new Window
            {
                Title = "Статистика по действиям пользователя",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var grid = new Grid();
            
            // Кнопки управления
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };
            
            var copyButton = new Button
            {
                Content = "Копировать для нейронки",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5)
            };
            
            var exportButton = new Button
            {
                Content = "Экспорт в файл",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5)
            };
            
            var clearButton = new Button
            {
                Content = "Очистить статистику",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5)
            };
            
            copyButton.Click += (s, e) => CopyStatisticsToClipboard();
            exportButton.Click += (s, e) => ExportStatisticsToFile();
            clearButton.Click += (s, e) => 
            {
                if (MessageBox.Show("Очистить всю статистику?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _userActions.Clear();
                    StartStatisticsCollection();
                    UpdateStatisticsUI();
                }
            };
            
            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(exportButton);
            buttonPanel.Children.Add(clearButton);
            
            // Текстовое поле для отображения статистики
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
            
            // Обновляем текст статистики
            UpdateStatisticsTextBox(textBox);
            
            // Разметка
            var row1 = new RowDefinition { Height = GridLength.Auto };
            var row2 = new RowDefinition { Height = new GridLength(1, GridUnitType.Star) };
            
            grid.RowDefinitions.Add(row1);
            grid.RowDefinitions.Add(row2);
            
            Grid.SetRow(buttonPanel, 0);
            Grid.SetRow(textBox, 1);
            
            grid.Children.Add(buttonPanel);
            grid.Children.Add(textBox);
            
            statsWindow.Content = grid;
            statsWindow.ShowDialog();
        }
        
        private void UpdateStatisticsUI()
        {
            // Метод для обновления UI статистики если нужно
            // Можно оставить пустым или добавить обновление счетчика в UI
        }
        
        private void UpdateStatisticsTextBox(TextBox textBox)
        {
            if (_userActions.Count == 0)
            {
                textBox.Text = "Статистика действий отсутствует.\nНачните просмотр видео для сбора данных.";
                return;
            }
            
            var sb = new StringBuilder();
            sb.AppendLine($"=== СТАТИСТИКА ПОЛЬЗОВАТЕЛЬСКИХ ДЕЙСТВИЙ ===\n");
            sb.AppendLine($"Видео: {Path.GetFileName(_currentVideoPath) ?? "Не загружено"}");
            sb.AppendLine($"Начало сессии: {_sessionStartTime:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine($"Длительность сессии: {(DateTime.Now - _sessionStartTime):hh\\:mm\\:ss}");
            sb.AppendLine($"Всего действий: {_userActions.Count}\n");
            
            sb.AppendLine("=== ПОДРОБНАЯ СТАТИСТИКА ===\n");
            
            var actionGroups = _userActions.GroupBy(a => a.ActionType)
                                          .Select(g => new { Type = g.Key, Count = g.Count() })
                                          .OrderByDescending(g => g.Count);
            
            sb.AppendLine("Сводка по типам действий:");
            foreach (var group in actionGroups)
            {
                sb.AppendLine($"  {group.Type}: {group.Count} раз");
            }
            
            sb.AppendLine("\n=== ХРОНОЛОГИЯ ДЕЙСТВИЙ ===\n");
            
            int index = 1;
            foreach (var action in _userActions)
            {
                sb.AppendLine($"{index:000}. {action}");
                if (action.AdditionalData != null)
                {
                    sb.AppendLine($"     Данные: {action.AdditionalData}");
                }
                index++;
            }
            
            sb.AppendLine("\n=== ДАННЫЕ ДЛЯ НЕЙРОННОЙ СЕТИ ===\n");
            sb.AppendLine(GetNeuralNetworkFormat());
            
            textBox.Text = sb.ToString();
        }
        
        private void CopyStatisticsToClipboard()
        {
            try
            {
                string neuralData = GetNeuralNetworkFormat();
                Clipboard.SetText(neuralData);
                MessageBox.Show("Данные скопированы в буфер обмена в формате для нейронной сети!", 
                              "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при копировании: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ExportStatisticsToFile()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON файлы (*.json)|*.json|Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                    FileName = $"user_stats_{DateTime.Now:yyyyMMdd_HHmmss}",
                    Title = "Экспорт статистики"
                };
                
                if (saveDialog.ShowDialog() == true)
                {
                    string fullData = GetStatisticsAsJson();
                    File.WriteAllText(saveDialog.FileName, fullData);
                    
                    MessageBox.Show($"Статистика экспортирована в файл: {saveDialog.FileName}", 
                                  "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private string GetNeuralNetworkFormat()
        {
            var sb = new StringBuilder();
            
            // Формат для нейронной сети
            sb.AppendLine("{");
            sb.AppendLine("  \"user_session\": {");
            sb.AppendLine($"    \"video\": \"{JsonEscape(Path.GetFileName(_currentVideoPath))}\",");
            sb.AppendLine($"    \"session_start\": \"{_sessionStartTime:yyyy-MM-ddTHH:mm:ss}\",");
            sb.AppendLine($"    \"total_actions\": {_userActions.Count},");
            
            // Группируем действия по типам
            var actionsByType = _userActions.GroupBy(a => a.ActionType)
                                           .ToDictionary(g => g.Key, g => g.Select(a => new
                                           {
                                               timestamp = a.Timestamp.ToString("HH:mm:ss"),
                                               position = $"{(int)a.VideoPosition.TotalSeconds}s",
                                               data = a.AdditionalData?.ToString()
                                           }).ToList());
            
            sb.AppendLine("    \"actions\": {");
            
            bool firstActionType = true;
            foreach (var kvp in actionsByType)
            {
                if (!firstActionType) sb.AppendLine(",");
                sb.AppendLine($"      \"{JsonEscape(kvp.Key)}\": [");
                
                bool firstAction = true;
                foreach (var action in kvp.Value)
                {
                    if (!firstAction) sb.AppendLine(",");
                    sb.Append($"        {{\"time\": \"{action.timestamp}\", \"pos\": \"{action.position}\"");
                    if (action.data != null)
                    {
                        sb.Append($", \"data\": \"{JsonEscape(action.data)}\"");
                    }
                    sb.Append("}");
                    firstAction = false;
                }
                sb.Append("\n      ]");
                firstActionType = false;
            }
            
            sb.AppendLine("\n    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
        
        private string JsonEscape(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            
            return input.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\n", "\\n")
                       .Replace("\r", "\\r")
                       .Replace("\t", "\\t")
                       .Replace("\b", "\\b")
                       .Replace("\f", "\\f");
        }
        
        private string GetStatisticsAsJson()
        {
            var data = new
            {
                session = new
                {
                    video = Path.GetFileName(_currentVideoPath),
                    session_start = _sessionStartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    total_actions = _userActions.Count,
                    actions = _userActions.Select(a => new
                    {
                        timestamp = a.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                        action_type = a.ActionType,
                        video_position = a.VideoPosition.TotalSeconds,
                        additional_data = a.AdditionalData?.ToString()
                    }).ToList()
                }
            };
            
            // Используем System.Text.Json
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            return JsonSerializer.Serialize(data, options);
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
            
            // Экспортируем статистику перед закрытием
            AutoExportStatistics();
        }
        
        private void AutoExportStatistics()
        {
            if (_userActions.Count > 0)
            {
                try
                {
                    string autoSavePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "EmotionAnalyzer",
                        "Statistics",
                        $"auto_save_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                    );
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(autoSavePath)!);
                    string jsonData = GetStatisticsAsJson();
                    File.WriteAllText(autoSavePath, jsonData);
                    
                    Console.WriteLine($"Статистика автоматически сохранена: {autoSavePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка автосохранения статистики: {ex.Message}");
                }
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
        
        // Методы для управления сбором статистики
        public void PauseStatisticsCollection()
        {
            _isCollectingStats = false;
            LogAction("stats_paused", _mediaPlayer?.Position ?? TimeSpan.Zero);
        }
        
        public void ResumeStatisticsCollection()
        {
            _isCollectingStats = true;
            LogAction("stats_resumed", _mediaPlayer?.Position ?? TimeSpan.Zero);
        }
        
        public void ClearStatistics()
        {
            _userActions.Clear();
            StartStatisticsCollection();
        }
        
        public List<UserAction> GetUserActions()
        {
            return new List<UserAction>(_userActions);
        }
        
        public string GetStatisticsSummary()
        {
            if (_userActions.Count == 0) return "Нет данных";
            
            var summary = $"Всего действий: {_userActions.Count}\n";
            var groups = _userActions.GroupBy(a => a.ActionType);
            
            foreach (var group in groups)
            {
                summary += $"{group.Key}: {group.Count()}\n";
            }
            
            return summary;
        }
         private void StatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            // Создаем окно калибровки
            _calibrationWindow = new CalibrationWindow();
            
            // Создаем модальное окно
            var calibrationDialog = new Window
            {
                Title = "Калибровка устройства",
                Content = _calibrationWindow,
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow
            };
            
            // Подписываемся на события калибровки
            _calibrationWindow.CalibrationCompleted += (s, args) =>
            {
                calibrationDialog.Close();
                MessageBox.Show("Калибровка завершена успешно!", "Готово", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                // Здесь можно открыть статистику
            };
            
            _calibrationWindow.CalibrationCancelled += (s, args) =>
            {
                calibrationDialog.Close();
                MessageBox.Show("Калибровка отменена", "Отменено", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };
            
            // Показываем окно и запускаем калибровку
            calibrationDialog.ShowDialog();
            _calibrationWindow.StartCalibration();
        }
    }
}