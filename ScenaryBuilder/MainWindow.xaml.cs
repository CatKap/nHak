using System;
using System.Windows;
using EmotionAnalyzer.Components;
using System.Windows.Media;

namespace EmotionAnalyzer
{
    public partial class MainWindow : Window
    {
        private VideoInfoPanel? _videoInfoPanel;
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Находим VideoInfoPanel в MainInterfaceGrid
            FindVideoInfoPanel();
            
            // Подписываемся на события
            if (StartupScreenComponent != null)
            {
                StartupScreenComponent.LoadingComplete += OnLoadingComplete;
            }
        }
        
        private void FindVideoInfoPanel()
        {
            // Находим VideoInfoPanel в визуальном дереве
            _videoInfoPanel = FindVisualChild<VideoInfoPanel>(MainInterfaceGrid);
            
            if (_videoInfoPanel == null)
            {
                // Если не нашли, попробуем другой способ
                _videoInfoPanel = MainInterfaceGrid.FindName("VideoInfoPanelComponent") as VideoInfoPanel;
            }
        }
        
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                    
                var childResult = FindVisualChild<T>(child);
                if (childResult != null)
                    return childResult;
            }
            return null;
        }
        
        private void OnLoadingComplete(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Получаем путь к видео из StartupScreen
                string videoPath = StartupScreenComponent.GetVideoPath();
                
                // Если нашли VideoInfoPanel, загружаем в него видео
                if (_videoInfoPanel != null && !string.IsNullOrEmpty(videoPath))
                {
                    _videoInfoPanel.LoadVideo(videoPath);
                }
                else if (!string.IsNullOrEmpty(videoPath))
                {
                    // Если не нашли через FindVisualChild, попробуем передать путь другим способом
                    MessageBox.Show("Видео загружено, но плеер не найден", 
                                  "Информация", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
                
                // Переключаем видимость
                StartupScreenComponent.Visibility = Visibility.Collapsed;
                MainInterfaceGrid.Visibility = Visibility.Visible;
            });
        }

        // Метод для сброса к начальному экрану
        public void ShowStartupScreen()
        {
            if (StartupScreenComponent != null)
            {
                StartupScreenComponent.Reset();
                StartupScreenComponent.Visibility = Visibility.Visible;
                MainInterfaceGrid.Visibility = Visibility.Collapsed;
                
                // Останавливаем видео при возврате на стартовый экран
                if (_videoInfoPanel != null)
                {
                    _videoInfoPanel.StopVideo();
                }
            }
        }
        
        // Простые обработчики с правильной сигнатурой
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Очищаем ресурсы видео перед закрытием
            if (_videoInfoPanel != null)
            {
                _videoInfoPanel.Cleanup();
            }
            
            this.Close();
        }
        
        // Метод для явной передачи видео в плеер (если нужно из другого места)
        public void LoadVideoIntoPlayer(string videoPath)
        {
            if (_videoInfoPanel != null && !string.IsNullOrEmpty(videoPath))
            {
                _videoInfoPanel.LoadVideo(videoPath);
            }
        }
    }
}