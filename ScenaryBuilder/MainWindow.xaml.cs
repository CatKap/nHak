using System;
using System.Windows;
using EmotionAnalyzer.Components;

namespace EmotionAnalyzer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Подписываемся на событие завершения загрузки видео
            StartupScreenComponent.VideoLoaded += OnVideoLoaded;
            StartupScreenComponent.LoadingComplete += OnVideoLoaded;
        }
        
        private void OnVideoLoaded(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StartupScreenComponent.Visibility = Visibility.Collapsed;
                MainInterfaceGrid.Visibility = Visibility.Visible;
            });
        }
        
        // Простые обработчики с правильной сигнатурой
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}