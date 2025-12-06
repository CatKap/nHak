using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;

namespace EmotionAnalyzer.Components
{
    public partial class StartupScreen : UserControl
    {
        // Событие загрузки видео
        public event EventHandler? VideoLoaded;
        
        // Событие полного завершения загрузки и обработки
        public event EventHandler? LoadingComplete;
        
        // Свойство для хранения пути к видео
        public string VideoPath { get; private set; } = string.Empty;
        
        public StartupScreen()
        {
            InitializeComponent();
            InitializeDropTarget();
        }

        private void InitializeDropTarget()
        {
            this.AllowDrop = true;
            
            this.DragEnter += OnDragEnter;
            this.DragOver += OnDragOver;
            this.DragLeave += OnDragLeave;
            this.Drop += OnDrop;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                this.Opacity = 0.8;
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            this.Opacity = 1.0;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            this.Opacity = 1.0;
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    var videoFile = files[0];
                    if (IsSupportedVideoFormat(videoFile))
                    {
                        await ProcessVideoFile(videoFile);
                    }
                    else
                    {
                        MessageBox.Show("Пожалуйста, выберите файл формата MP4 или MOV.", 
                                      "Неверный формат", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Warning);
                    }
                }
            }
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Видео файлы (*.mp4;*.mov)|*.mp4;*.mov|Все файлы (*.*)|*.*",
                Multiselect = false,
                Title = "Выберите видео файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await ProcessVideoFile(openFileDialog.FileName);
            }
        }

        private bool IsSupportedVideoFormat(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension == ".mp4" || extension == ".mov";
        }

        private async Task ProcessVideoFile(string filePath)
        {
            try
            {
                MainPanel.Visibility = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                
                LoadingProgressBar.Value = 0;
                ProgressPercentageText.Text = "0%";
                StatusTextBlock.Text = "Подготовка к загрузке...";

                await SimulateVideoLoading(filePath);
                
                // Сохраняем путь к видео
                VideoPath = filePath;
                StatusTextBlock.Text = "Видео успешно загружено!";
                await Task.Delay(500);
                
                // Генерируем событие загрузки видео
                VideoLoaded?.Invoke(this, EventArgs.Empty);
                
                // Дополнительная обработка после загрузки видео
                await ProcessAfterVideoLoaded();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке видео: {ex.Message}", 
                              "Ошибка", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
                
                MainPanel.Visibility = Visibility.Visible;
                LoadingPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async Task SimulateVideoLoading(string filePath)
        {
            // Этап 1: Проверка файла
            StatusTextBlock.Text = "Проверка файла...";
            for (int i = 0; i <= 10; i++)
            {
                LoadingProgressBar.Value = i;
                ProgressPercentageText.Text = $"{i}%";
                await Task.Delay(30);
            }

            // Этап 2: Загрузка видео
            StatusTextBlock.Text = "Загрузка видео...";
            for (int i = 11; i <= 40; i++)
            {
                LoadingProgressBar.Value = i;
                ProgressPercentageText.Text = $"{i}%";
                await Task.Delay(40);
            }

            // Этап 3: Анализ метаданных
            StatusTextBlock.Text = "Анализ метаданных...";
            for (int i = 41; i <= 70; i++)
            {
                LoadingProgressBar.Value = i;
                ProgressPercentageText.Text = $"{i}%";
                await Task.Delay(30);
            }

            // Этап 4: Подготовка к воспроизведению
            StatusTextBlock.Text = "Подготовка к воспроизведению...";
            for (int i = 71; i <= 100; i++)
            {
                LoadingProgressBar.Value = i;
                ProgressPercentageText.Text = $"{i}%";
                await Task.Delay(20);
            }
        }
        
        private async Task ProcessAfterVideoLoaded()
        {
            try
            {
                // Имитация дополнительной обработки
                StatusTextBlock.Text = "Выполнение анализа...";
                LoadingProgressBar.Value = 0;
                
                for (int i = 0; i <= 100; i += 10)
                {
                    LoadingProgressBar.Value = i;
                    ProgressPercentageText.Text = $"{i}%";
                    await Task.Delay(100);
                }
                
                StatusTextBlock.Text = "Анализ завершен!";
                await Task.Delay(500);
                
                // Генерируем событие полного завершения
                LoadingComplete?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при анализе: {ex.Message}", 
                              "Ошибка", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
                
                // Все равно вызываем завершение
                LoadingComplete?.Invoke(this, EventArgs.Empty);
            }
        }
        
        // Публичный метод для получения пути к видео
        public string GetVideoPath()
        {
            return VideoPath;
        }
        
        // Метод для сброса состояния
        public void Reset()
        {
            VideoPath = string.Empty;
            MainPanel.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
            LoadingProgressBar.Value = 0;
            ProgressPercentageText.Text = "0%";
            StatusTextBlock.Text = "Готов к загрузке видео";
        }
    }
}