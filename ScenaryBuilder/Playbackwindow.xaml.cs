using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScenaryBuilder
{
    public partial class PlaybackWindow : Window
    {
        private bool isPlaying = false;
        private bool isDraggingProgress = false;
        
        public PlaybackWindow()
        {
            InitializeComponent();
            InitializeVideoPlayer();
        }
        
        private void InitializeVideoPlayer()
        {
            // Начальные настройки
            VideoPlayer.Volume = 0.5;
            UpdateTimeDisplay();
        }
        
        // === ОБРАБОТЧИКИ СОБЫТИЙ ===
        
        // 1. Загрузка видео
        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.avi;*.mov;*.wmv;*.mkv;*.flv|All Files|*.*",
                Title = "Select Video File",
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
                PlayPauseButton.Content = "▶ Play";
                
                // Загружаем новое видео
                VideoPlayer.Source = new Uri(filePath);
                
                // Обновляем заголовок окна
                Title = $"Video Playback - {System.IO.Path.GetFileName(filePath)}";
                
                // Сбрасываем прогресс
                ProgressBar.Value = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading video:\n{ex.Message}", 
                    "Load Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
        
        // 2. Двойной клик по видео
        private void VideoPlayer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            LoadButton_Click(sender, e);
        }
        
        // 3. Воспроизведение/пауза
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source == null)
            {
                MessageBox.Show("Please load a video first!", 
                    "No Video", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
                return;
            }
            
            if (isPlaying)
            {
                VideoPlayer.Pause();
                PlayPauseButton.Content = "▶ Play";
            }
            else
            {
                VideoPlayer.Play();
                PlayPauseButton.Content = "⏸ Pause";
            }
            
            isPlaying = !isPlaying;
        }
        
        // 4. Стоп
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Stop();
            isPlaying = false;
            PlayPauseButton.Content = "▶ Play";
            ProgressBar.Value = 0;
            UpdateTimeDisplay();
        }
        
        // 5. Громкость
        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            // Простой контроль громкости
            if (VideoPlayer.Volume == 0)
            {
                VideoPlayer.Volume = 0.5;
                ((Button)sender).Content = "🔊 Volume";
            }
            else if (VideoPlayer.Volume == 0.5)
            {
                VideoPlayer.Volume = 1.0;
                ((Button)sender).Content = "🔊 Max";
            }
            else
            {
                VideoPlayer.Volume = 0;
                ((Button)sender).Content = "🔇 Muted";
            }
        }
        
        // 6. Когда видео загружено
        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var duration = VideoPlayer.NaturalDuration.TimeSpan;
                ProgressBar.Maximum = duration.TotalSeconds;
                TotalTimeText.Text = duration.ToString(@"mm\:ss");
                
                // Запускаем таймер обновления
                CompositionTarget.Rendering += UpdateProgress;
            }
        }
        
        // 7. Обновление прогресса
        private void UpdateProgress(object sender, EventArgs e)
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan && !isDraggingProgress)
            {
                ProgressBar.Value = VideoPlayer.Position.TotalSeconds;
                UpdateTimeDisplay();
            }
        }
        
        // 8. Когда видео закончилось
        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            isPlaying = false;
            PlayPauseButton.Content = "▶ Play";
            ProgressBar.Value = 0;
            UpdateTimeDisplay();
            CompositionTarget.Rendering -= UpdateProgress;
        }
        
        // 9. Перетаскивание прогресс-бара
        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VideoPlayer.Source != null && isDraggingProgress)
            {
                VideoPlayer.Position = TimeSpan.FromSeconds(e.NewValue);
                UpdateTimeDisplay();
            }
        }
        
        // 10. Обновление отображения времени
        private void UpdateTimeDisplay()
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                CurrentTimeText.Text = VideoPlayer.Position.ToString(@"mm\:ss");
                
                // Если видео загружено, показываем общее время
                if (VideoPlayer.Source != null && TotalTimeText.Text == "00:00")
                {
                    TotalTimeText.Text = VideoPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
                }
            }
        }
        
        // 11. Обработка мыши на прогресс-баре
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            
            if (e.OriginalSource is ProgressBar)
            {
                isDraggingProgress = true;
                CaptureMouse();
            }
        }
        
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            
            if (isDraggingProgress)
            {
                isDraggingProgress = false;
                ReleaseMouseCapture();
            }
        }
        
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            if (isDraggingProgress && VideoPlayer.Source != null)
            {
                var progressBar = ProgressBar;
                var mousePosition = e.GetPosition(progressBar);
                var percent = mousePosition.X / progressBar.ActualWidth;
                ProgressBar.Value = percent * ProgressBar.Maximum;
            }
        }
        
        // 12. Очистка при закрытии
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // Останавливаем таймер
            CompositionTarget.Rendering -= UpdateProgress;
            
            // Очищаем ресурсы видео
            VideoPlayer.Close();
            VideoPlayer.Source = null;
        }
        
        // 13. Публичный метод для загрузки видео извне
        public void OpenVideo(string videoPath)
        {
            if (System.IO.File.Exists(videoPath))
            {
                LoadVideoFile(videoPath);
            }
            else
            {
                MessageBox.Show($"Video file not found:\n{videoPath}", 
                    "File Not Found", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
            }
        }
    }
}