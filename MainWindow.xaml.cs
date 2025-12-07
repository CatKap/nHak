using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using EmotionAnalyzer.Components;
using System.Windows.Media;
using WpfApp2;

namespace EmotionAnalyzer
{
    public partial class MainWindow : Window
    {
        private VideoInfoPanel? _videoInfoPanel;
        private static bool _isDiagnosticWindowOpen = false;
        private static Instruction _diagnosticWindowInstance;
        public MainWindow()
        {
            InitializeComponent();
            Console.OutputEncoding = Encoding.UTF8;

            // Находим VideoInfoPanel в MainInterfaceGrid
            FindVideoInfoPanel();
            
            // Подписываемся на события
            if (StartupScreenComponent != null)
            {
                StartupScreenComponent.LoadingComplete += OnLoadingComplete;
            }
        }

        private void StartDiagnostic(object sender, RoutedEventArgs e)
        {
            // Если окно уже открыто, активируем его
            if (_isDiagnosticWindowOpen && _diagnosticWindowInstance != null)
            {
                _diagnosticWindowInstance.Activate();
                _diagnosticWindowInstance.Focus();
                return;
            }
    
            // Создаем новое окно
            _diagnosticWindowInstance = new Instruction();
            _isDiagnosticWindowOpen = true;
    
            // Подписываемся на событие закрытия окна
            _diagnosticWindowInstance.Closed += (s, args) =>
            {
                _isDiagnosticWindowOpen = false;
                _diagnosticWindowInstance = null;
            };
    
            _diagnosticWindowInstance.Show();
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