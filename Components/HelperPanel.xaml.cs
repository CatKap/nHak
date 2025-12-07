using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Documents;
using System.Collections.Generic;
using CsToPy;
using ScenaryBuilder.Components;

namespace EmotionAnalyzer.Components
{
    public partial class HelperPanel : UserControl
    {
        private static HelperPanel? _instance;
        public static HelperPanel Instance => _instance ??= new HelperPanel();
        private AiAnalytics anal;
        public string Markdown;
        public string  DocumentStyle;
        
        // Свойство для хранения текущего отчета
        private string _currentReport = string.Empty;
        
        public HelperPanel()
        {
            InitializeComponent();
            anal = new AiAnalytics();
            _instance = this; // Устанавливаем инстанс
            MarkdownViewer.Markdown = "Сначала соберите статистику";

        }
        
        // Метод для установки отчета от нейронной сети
        public void SetNeuralNetworkReport(string report)
        {
            if (string.IsNullOrEmpty(report))
            {
                Console.WriteLine("Получен пустой отчет от нейронной сети");
                return;
            }
            
            Console.WriteLine($"Получен отчет от нейронной сети ({report.Length} символов):");
            Console.WriteLine(report.Substring(0, Math.Min(200, report.Length)) + "...");
            
            _currentReport = report;
            
     
                // Показываем уведомление пользователю
            var netReport = new NeuroStats();
            Markdown = _currentReport;
            netReport.Show();
        }
        
        public void setMarkdown(string text)
        {
            if (Dispatcher.CheckAccess())
            {
                // Мы в UI-потоке
                MarkdownViewer.Markdown = text;
            }
            else
            {
                // Мы не в UI-потоке, используем Dispatcher
                Dispatcher.Invoke(() => MarkdownViewer.Markdown = text);
            }
        }
        
        
        private void UpdateReportDisplay()
        {
            // Показываем только первые 200 символов отчета в AverageMetric
            if (!string.IsNullOrEmpty(_currentReport))
            {
                string preview = _currentReport.Length > 200 
                    ? _currentReport.Substring(0, 200) + "..." 
                    : _currentReport;
            }
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
            Console.WriteLine("Trying to get statistics");
            
            // Открытие окна с полным отчётом анализа
            if (!string.IsNullOrEmpty(_currentReport))
            {
                ShowFullReportWindow();
            }
            else
            {
                // Если отчета еще нет, запрашиваем его
                MessageBox.Show("Запрашиваем анализ данных...", "Запрос анализа", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Вызываем анализ через AiAnalytics
                anal.requestAnalytics(RealTimeDataManager.Instance.GetCompactJson());
            }
        }
        
        // Метод для отображения окна с полным отчетом
        private void ShowFullReportWindow()
        {
            var reportWindow = new Window
            {
                Title = "📊 Полный отчет нейронной сети",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ShowInTaskbar = true,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                Background = Brushes.WhiteSmoke
            };
            
            // Создаем основной Grid
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Заголовок
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(15),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var titleText = new TextBlock
            {
                Text = "🔍 Анализ эмоционального состояния",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Navy,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            
            var subtitleText = new TextBlock
            {
                Text = "Отчет сгенерирован нейронной сетью",
                FontSize = 12,
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic,
                TextAlignment = TextAlignment.Center
            };
            
            var timestampText = new TextBlock
            {
                Text = $"Дата: {DateTime.Now:dd.MM.yyyy HH:mm:ss}",
                FontSize = 11,
                Foreground = Brushes.DarkGray,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            
            headerPanel.Children.Add(titleText);
            headerPanel.Children.Add(subtitleText);
            headerPanel.Children.Add(timestampText);
            
            // Основное текстовое поле для отчета
            var scrollViewer = new ScrollViewer
            {
                Margin = new Thickness(10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(5)
            };
            
            var reportTextBox = new TextBox
            {
                Text = _currentReport,
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Background = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            
            scrollViewer.Content = reportTextBox;
            
            // Панель кнопок
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };
            
            var copyButton = new Button
            {
                Content = "📋 Копировать",
                Width = 120,
                Height = 32,
                Margin = new Thickness(5),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E90FF")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                ToolTip = "Скопировать отчет в буфер обмена"
            };
            
            var exportButton = new Button
            {
                Content = "💾 Сохранить",
                Width = 120,
                Height = 32,
                Margin = new Thickness(5),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#32CD32")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                ToolTip = "Сохранить отчет в текстовый файл"
            };
            
            var closeButton = new Button
            {
                Content = "✕ Закрыть",
                Width = 100,
                Height = 32,
                Margin = new Thickness(5),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC143C")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            
            copyButton.Click += (s, e) => 
            {
                try
                {
                    Clipboard.SetText(_currentReport);
                    MessageBox.Show("Отчет скопирован в буфер обмена!", 
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при копировании: {ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            exportButton.Click += (s, e) => 
            {
                try
                {
                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                        FileName = $"neural_report_{DateTime.Now:yyyyMMdd_HHmmss}",
                        Title = "Сохранить отчет нейронной сети",
                        DefaultExt = ".txt"
                    };
                    
                    if (saveDialog.ShowDialog() == true)
                    {
                        // Добавляем заголовок к отчету
                        string fullReport = $"=== ОТЧЕТ НЕЙРОННОЙ СЕТИ ===\n" +
                                           $"Дата генерации: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\n" +
                                           $"Длина отчета: {_currentReport.Length} символов\n" +
                                           $"=================================\n\n" +
                                           _currentReport;
                        
                        System.IO.File.WriteAllText(saveDialog.FileName, fullReport);
                        
                        MessageBox.Show($"Отчет сохранен в файл:\n{saveDialog.FileName}", 
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении: {ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            closeButton.Click += (s, e) => reportWindow.Close();
            
            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(exportButton);
            buttonPanel.Children.Add(closeButton);
            
            // Размещаем элементы
            Grid.SetRow(headerPanel, 0);
            Grid.SetRow(scrollViewer, 1);
            Grid.SetRow(buttonPanel, 2);
            
            mainGrid.Children.Add(headerPanel);
            mainGrid.Children.Add(scrollViewer);
            mainGrid.Children.Add(buttonPanel);
            
            reportWindow.Content = mainGrid;
            reportWindow.Show();
        }
        
        // Метод для получения текущего отчета
        public string GetCurrentReport()
        {
            return _currentReport;
        }
       
        
        // Метод для проверки, есть ли отчет
        public bool HasReport()
        {
            return !string.IsNullOrEmpty(_currentReport);
        }
    }
}