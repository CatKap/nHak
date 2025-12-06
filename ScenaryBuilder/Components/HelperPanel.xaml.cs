using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace EmotionAnalyzer.Components
{
    public partial class HelperPanel : UserControl
    {
        public HelperPanel()
        {
            InitializeComponent();
        }

        // Событие для нейроанализа
        public event RoutedEventHandler NeuroAnalysisClicked;

        // Обработчик кнопки нейроанализа
        private void NeuroAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            NeuroAnalysisClicked?.Invoke(sender, e);
        }

        // Метод для добавления временных меток высокой активности из бэка
        public void AddHighActivityTimestamp(string timestamp)
        {
            // Создание кнопки с временной меткой
            // Будет реализовано другими разработчиками
        }

        // Метод для добавления временных меток низкой активности из бэка
        public void AddLowActivityTimestamp(string timestamp)
        {
            // Создание кнопки с временной меткой
            // Будет реализовано другими разработчиками
        }

        // Метод для обновления среднего показателя
        public void UpdateAverageMetric(string metric)
        {
            AverageMetric.Text = metric;
        }

        // Метод для очистки всех временных меток
        public void ClearTimestamps()
        {
            HighActivityTimestamps.Children.Clear();
            LowActivityTimestamps.Children.Clear();
        }

        // Обработчик нажатия на временную метку
        private void TimestampButton_Click(object sender, RoutedEventArgs e)
        {
            // Переход к указанному времени в видео
            // Будет реализовано другими разработчиками
        }
    }
}