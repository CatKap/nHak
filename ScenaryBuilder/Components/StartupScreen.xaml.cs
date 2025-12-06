using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EmotionAnalyzer.Components
{
    public partial class StartupScreen : UserControl
    {
        private DispatcherTimer? _loadingTimer; // Добавил nullable
        private bool _isLoading = false;
        private Random _random = new Random();

        // События для уведомления о загрузке видео
        public event EventHandler? VideoLoaded; // Добавил nullable
        public event EventHandler? LoadingComplete; // Добавил nullable

        public StartupScreen()
        {
            InitializeComponent();
            InitializeLoadingTimer();
        }

        private void InitializeLoadingTimer()
        {
            _loadingTimer = new DispatcherTimer();
            _loadingTimer.Interval = TimeSpan.FromMilliseconds(100);
            _loadingTimer.Tick += LoadingTimer_Tick;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            StartVideoLoading();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            parentWindow?.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.WindowState = WindowState.Minimized;
            }
        }

        public void StartVideoLoading()
        {
            if (_isLoading) return;

            _isLoading = true;

            // Переключаемся на экран загрузки
            MainPanel.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;

            // Сбрасываем прогресс
            LoadingProgressBar.Value = 0;
            ProgressPercentageText.Text = "0%";
            StatusTextBlock.Text = "Начинаем загрузку видео...";

            // Запускаем таймер
            _loadingTimer?.Start();
        }

        private void LoadingTimer_Tick(object? sender, EventArgs e) // Добавил nullable для sender
        {
            if (!_isLoading) return;

            // Увеличиваем прогресс
            int increment = _random.Next(1, 5);
            double newValue = LoadingProgressBar.Value + increment;

            if (newValue >= 100)
            {
                // Загрузка завершена
                LoadingProgressBar.Value = 100;
                ProgressPercentageText.Text = "100%";
                StatusTextBlock.Text = "Загрузка завершена! Обработка видео...";

                // Ждем 2 секунды
                _loadingTimer?.Stop();
                Task.Delay(2000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Вызываем события о завершении загрузки
                        VideoLoaded?.Invoke(this, EventArgs.Empty);
                        LoadingComplete?.Invoke(this, EventArgs.Empty);
                        
                        _isLoading = false;
                    });
                });
            }
            else
            {
                // Обновляем прогресс
                LoadingProgressBar.Value = newValue;
                ProgressPercentageText.Text = $"{(int)newValue}%";

                // Обновляем статус
                UpdateLoadingStatus(newValue);
            }
        }

        private void UpdateLoadingStatus(double progress)
        {
            if (progress < 20)
                StatusTextBlock.Text = "Анализ видеофайла...";
            else if (progress < 40)
                StatusTextBlock.Text = "Чтение метаданных...";
            else if (progress < 60)
                StatusTextBlock.Text = "Загрузка видеопотока...";
            else if (progress < 80)
                StatusTextBlock.Text = "Извлечение аудиодорожки...";
            else if (progress < 95)
                StatusTextBlock.Text = "Финальная обработка...";
            else
                StatusTextBlock.Text = "Завершение загрузки...";
        }
        
        // Метод для сброса экрана загрузки (если нужен возврат)
        public void ResetLoadingScreen()
        {
            _isLoading = false;
            _loadingTimer?.Stop();
            
            MainPanel.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
            
            LoadingProgressBar.Value = 0;
            ProgressPercentageText.Text = "0%";
            StatusTextBlock.Text = "Подготовка к загрузке...";
        }
        
        // Публичный метод для симуляции загрузки видео (для тестов)
        public void SimulateVideoLoad()
        {
            StartVideoLoading();
        }
    }
}