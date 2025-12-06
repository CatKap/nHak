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
            _videoInfoPanel = MainInterfaceGrid.FindName("VideoInfoPanelComponent") as VideoInfoPanel;
        }
        
        private void OnLoadingComplete(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string videoPath = StartupScreenComponent.GetVideoPath();
                
                if (_videoInfoPanel != null && !string.IsNullOrEmpty(videoPath))
                {
                    _videoInfoPanel.LoadVideo(videoPath);
                }
                else if (!string.IsNullOrEmpty(videoPath))
                {
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

        // Обработчики кнопок из FeedBackPanel
        private void FeedBackPanel_OpenFullResultsClicked(object sender, RoutedEventArgs e)
        {
            // Логика открытия полных результатов
            MessageBox.Show("Открытие полных результатов...");
        }
        
        private void FeedBackPanel_OpenFeedbackClicked(object sender, RoutedEventArgs e)
        {
            // Скрываем HelperPanel и FeedBackPanel
            HelperPanelComponent.Visibility = Visibility.Collapsed;
            FeedBackPanelComponent.Visibility = Visibility.Collapsed;
            
            // Показываем форму обратной связи на весь левый столбец
            FeedbackFormComponent.Visibility = Visibility.Visible;
        }
        
        // Обработчики формы обратной связи
        private void FeedbackForm_FormSubmitted(object sender, RoutedEventArgs e)
        {
            // Возвращаемся к исходному виду
            FeedbackFormComponent.Visibility = Visibility.Collapsed;
            HelperPanelComponent.Visibility = Visibility.Visible;
            FeedBackPanelComponent.Visibility = Visibility.Visible;
            
            MessageBox.Show("Спасибо за обратную связь!", "Успех");
        }
        
        private void FeedbackForm_FormCancelled(object sender, RoutedEventArgs e)
        {
            // Возвращаемся к исходному виду
            FeedbackFormComponent.Visibility = Visibility.Collapsed;
            HelperPanelComponent.Visibility = Visibility.Visible;
            FeedBackPanelComponent.Visibility = Visibility.Visible;
        }
        
        public void ShowStartupScreen()
        {
            if (StartupScreenComponent != null)
            {
                StartupScreenComponent.Reset();
                StartupScreenComponent.Visibility = Visibility.Visible;
                MainInterfaceGrid.Visibility = Visibility.Collapsed;
                
                if (_videoInfoPanel != null)
                {
                    _videoInfoPanel.StopVideo();
                }
                
                // Скрываем форму обратной связи при возврате
                FeedbackFormComponent.Visibility = Visibility.Collapsed;
                HelperPanelComponent.Visibility = Visibility.Visible;
                FeedBackPanelComponent.Visibility = Visibility.Visible;
            }
        }
        
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_videoInfoPanel != null)
            {
                _videoInfoPanel.Cleanup();
            }
            
            this.Close();
        }
        
        public void LoadVideoIntoPlayer(string videoPath)
        {
            if (_videoInfoPanel != null && !string.IsNullOrEmpty(videoPath))
            {
                _videoInfoPanel.LoadVideo(videoPath);
            }
        }
    }
}