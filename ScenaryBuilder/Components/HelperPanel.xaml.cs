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
            // Обновление текста среднего показателя
            // Будет реализовано другими разработчиками
        }

        // Метод для очистки всех временных меток
        public void ClearTimestamps()
        {
            // Очистка контейнеров с метками
            // Будет реализовано другими разработчиками
        }

        // Обработчик нажатия на временную метку
        private void TimestampButton_Click(object sender, RoutedEventArgs e)
        {
            // Переход к указанному времени в видео
            // Будет реализовано другими разработчиками
        }

        // Обработчик кнопки открытия полного результата
        private void OpenFullResultsButton_Click(object sender, RoutedEventArgs e)
        {
            // Открытие окна с полным отчётом анализа
            // Будет реализовано другими разработчиками
        }
        // Обработчик открытия формы обратной связи

        // Этот метод ОБЯЗАТЕЛЬНО должен быть
        private void OpenFeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            // Просто переключаем видимость
            if (AnalyticsPanel != null && FeedbackFormComponent != null)
            {
                AnalyticsPanel.Visibility = Visibility.Collapsed;
                FeedbackFormComponent.Visibility = Visibility.Visible;
            }
        }

        // Эти методы тоже должны быть
        private void FeedbackForm_Submitted(object sender, RoutedEventArgs e)
        {
            if (FeedbackFormComponent != null && AnalyticsPanel != null)
            {
                FeedbackFormComponent.Visibility = Visibility.Collapsed;
                AnalyticsPanel.Visibility = Visibility.Visible;
            }
        }

        private void FeedbackForm_Cancelled(object sender, RoutedEventArgs e)
        {
            if (FeedbackFormComponent != null && AnalyticsPanel != null)
            {
                FeedbackFormComponent.Visibility = Visibility.Collapsed;
                AnalyticsPanel.Visibility = Visibility.Visible;
            }
        }
    }
}
