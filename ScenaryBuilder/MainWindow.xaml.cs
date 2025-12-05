using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EmotionAnalyzer  // ИЛИ ScenaryBuilder - смотрите namespace в XAML
{
    public partial class MainWindow : Window
    {
        private bool isPlaying = false;
        private bool isDraggingProgress = false;
        private DispatcherTimer progressTimer;
        
        public MainWindow()
        {
            InitializeComponent();
            InitializeVideoPlayer();
        }
        
        private void InitializeVideoPlayer()
        {
            // Настройка таймера для обновления прогресса
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromMilliseconds(100);
            progressTimer.Tick += ProgressTimer_Tick;
            
            // Начальные настройки
            VideoPlayer.Volume = 0.5;
            UpdateTimeDisplay();
            
            // Подключаем обработчики событий
            LoadVideoButton.Click += LoadButton_Click;
            PlayButton.Click += PlayButton_Click;
            PauseButton.Click += PauseButton_Click;
            StopButton.Click += StopButton_Click;
            RewindButton.Click += RewindButton_Click;
            FastForwardButton.Click += FastForwardButton_Click;
            VideoTimeline.ValueChanged += VideoTimeline_ValueChanged;
        }
        
        // === ОБРАБОТЧИКИ СОБЫТИЙ ===
        
        // Загрузка видео
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.wmv;*.mkv|All Files|*.*",
                Title = "Выберите видео файл",
                Multiselect = false
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                LoadVideoFile(openFileDialog.FileName);
            }
        }
        
        private void LoadVideoFile(string filePath)
        {
            try
            {
                // Останавливаем текущее воспроизведение
                VideoPlayer.Stop();
                isPlaying = false;
                PlayButton.Content = "▶";
                
                // Загружаем новое видео
                VideoPlayer.Source = new Uri(filePath);
                
                // Обновляем интерфейс
                VideoFileNameText.Text = System.IO.Path.GetFileName(filePath);
                StatusText.Text = "Видео загружено";
                
                // Сбрасываем прогресс
                VideoTimeline.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки видео:\n{ex.Message}", 
                    "Ошибка", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
        
        // Воспроизведение
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source == null)
            {
                MessageBox.Show("Пожалуйста, загрузите видео сначала!", 
                    "Нет видео", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
                return;
            }
            
            VideoPlayer.Play();
            isPlaying = true;
            PlayButton.Content = "⏸";
            progressTimer.Start();
            StatusText.Text = "Воспроизведение";
        }
        
        // Пауза
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
            {
                VideoPlayer.Pause();
                isPlaying = false;
                PlayButton.Content = "▶";
                progressTimer.Stop();
                StatusText.Text = "Пауза";
            }
        }
        
        // Стоп
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Stop();
            isPlaying = false;
            PlayButton.Content = "▶";
            VideoTimeline.Value = 0;
            progressTimer.Stop();
            UpdateTimeDisplay();
            StatusText.Text = "Остановлено";
        }
        
        // Перемотка назад
        private void RewindButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source != null)
            {
                var newPosition = VideoPlayer.Position - TimeSpan.FromSeconds(10);
                if (newPosition < TimeSpan.Zero) newPosition = TimeSpan.Zero;
                VideoPlayer.Position = newPosition;
            }
        }
        
        // Перемотка вперед
        private void FastForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var newPosition = VideoPlayer.Position + TimeSpan.FromSeconds(10);
                var maxDuration = VideoPlayer.NaturalDuration.TimeSpan;
                if (newPosition > maxDuration) newPosition = maxDuration;
                VideoPlayer.Position = newPosition;
            }
        }
        
        // Обновление прогресса через таймер
        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan && !isDraggingProgress)
            {
                var totalSeconds = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                var currentSeconds = VideoPlayer.Position.TotalSeconds;
                
                if (totalSeconds > 0)
                {
                    VideoTimeline.Value = (currentSeconds / totalSeconds) * 100;
                    UpdateTimeDisplay();
                }
            }
        }
        
        // Перетаскивание ползунка времени
        private void VideoTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                if (isDraggingProgress)
                {
                    var totalSeconds = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    var newPosition = TimeSpan.FromSeconds((e.NewValue / 100.0) * totalSeconds);
                    VideoPlayer.Position = newPosition;
                }
                UpdateTimeDisplay();
            }
        }
        
        // Обновление отображения времени
        private void UpdateTimeDisplay()
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                CurrentTimeText.Text = VideoPlayer.Position.ToString(@"mm\:ss");
                TotalTimeText.Text = VideoPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
                
                TimeDisplayText.Text = $"{CurrentTimeText.Text} / {TotalTimeText.Text}";
            }
            else
            {
                CurrentTimeText.Text = "00:00";
                TotalTimeText.Text = "00:00";
                TimeDisplayText.Text = "--:-- / --:--";
            }
        }
        
        // Обработка мыши на слайдере
        private void VideoTimeline_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingProgress = true;
            VideoPlayer.Pause();
            progressTimer.Stop();
        }
        
        private void VideoTimeline_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDraggingProgress = false;
            if (isPlaying)
            {
                VideoPlayer.Play();
                progressTimer.Start();
            }
        }
        
        // Двойной клик по видео
        private void VideoPlayer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            LoadButton_Click(sender, e);
        }
        
        // Когда видео загружено
        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                VideoTimeline.Maximum = 100;
                UpdateTimeDisplay();
            }
        }
        
        // Когда видео закончилось
        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            isPlaying = false;
            PlayButton.Content = "▶";
            VideoTimeline.Value = 0;
            progressTimer.Stop();
            UpdateTimeDisplay();
            StatusText.Text = "Воспроизведение завершено";
        }
        
        // Очистка при закрытии
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            progressTimer?.Stop();
            VideoPlayer.Close();
        }
    }
}