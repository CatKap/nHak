using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Documents;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Win32;
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
        public string DocumentStyle;
        
        // Свойство для хранения текущего отчета
        private string _currentReport = string.Empty;
        
        public HelperPanel()
        {
            InitializeComponent();
            
            // Инициализация AI асинхронно, чтобы не блокировать UI
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        anal = new AiAnalytics();
                        Console.WriteLine("AI Analytics инициализирован");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка инициализации AI: {ex.Message}");
                    }
                });
            });
            
            _instance = this; // Устанавливаем инстанс
            MarkdownViewer.Markdown = "Сначала соберите статистику";
        }
        
        private async void OpenGroupResultsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("Открытие диалога выбора файлов для группового анализа...");
                
                // Создаем диалог выбора файлов
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Title = "Выберите файлы для группового анализа",
                    Multiselect = true,
                    Filter = "JSON файлы (*.json)|*.json|Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                
                // Показываем диалог
                bool? result = openFileDialog.ShowDialog();
                
                if (result == true && openFileDialog.FileNames.Length > 0)
                {
                    Console.WriteLine($"Выбрано файлов: {openFileDialog.FileNames.Length}");
                    
                    // Показываем прогресс
                    MessageBox.Show($"Выбрано {openFileDialog.FileNames.Length} файлов. Начинаю обработку...", 
                        "Групповой анализ", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Процессинг выбранных файлов
                    await ProcessMultipleFiles(openFileDialog.FileNames);
                }
                else
                {
                    Console.WriteLine("Выбор файлов отменен");
                    MessageBox.Show("Выбор файлов отменен.", "Информация", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при выборе файлов: {ex.Message}");
                MessageBox.Show($"Ошибка при выборе файлов: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async Task ProcessMultipleFiles(string[] filePaths)
        {
            try
            {
                // Проверяем, инициализирован ли AI
                if (anal == null)
                {
                    MessageBox.Show("AI аналитика еще не готова. Подождите немного...", 
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                StringBuilder combinedData = new StringBuilder();
                combinedData.AppendLine("=== ГРУППОВОЙ АНАЛИЗ ===");
                combinedData.AppendLine($"Количество файлов: {filePaths.Length}");
                combinedData.AppendLine($"Дата анализа: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                combinedData.AppendLine("=".PadRight(50, '='));
                combinedData.AppendLine();
                
                int processedFiles = 0;
                
                foreach (string filePath in filePaths)
                {
                    try
                    {
                        string fileName = Path.GetFileName(filePath);
                        Console.WriteLine($"Обработка файла: {fileName}");
                        
                        // Читаем содержимое файла с указанием кодировки UTF-8
                        string fileContent = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                        
                        // Преобразуем JSON в читаемый текст
                        string cleanContent = ConvertJsonToReadableText(fileContent, fileName);
                        
                        // Добавляем информацию о файле
                        combinedData.AppendLine($"\n--- ФАЙЛ: {fileName} ---");
                        combinedData.AppendLine($"Размер: {new FileInfo(filePath).Length} байт");
                        combinedData.AppendLine($"Изменен: {File.GetLastWriteTime(filePath):dd.MM.yyyy HH:mm}");
                        combinedData.AppendLine($"Тип: {Path.GetExtension(filePath).ToUpper()}");
                        combinedData.AppendLine("-".PadRight(40, '-'));
                        
                        // Добавляем очищенное содержимое
                        combinedData.AppendLine(cleanContent);
                        combinedData.AppendLine();
                        
                        processedFiles++;
                        
                        // Обновляем статус
                        Dispatcher.Invoke(() =>
                        {
                            MarkdownViewer.Markdown = $"## Обработка файлов\n\n" +
                                                     $"✅ Обработано: {processedFiles}/{filePaths.Length}\n" +
                                                     $"📁 Текущий файл: {fileName}";
                        });
                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка обработки файла {filePath}: {ex.Message}");
                        
                        combinedData.AppendLine($"\n[ОШИБКА] при обработке файла {Path.GetFileName(filePath)}:");
                        combinedData.AppendLine($"   {ex.Message}");
                        combinedData.AppendLine();
                    }
                    
                    // Небольшая задержка между файлами
                    await Task.Delay(50);
                }
                
                // Формируем финальный результат
                combinedData.AppendLine("\n" + "=".PadRight(50, '='));
                combinedData.AppendLine($"ИТОГО: Успешно обработано {processedFiles} из {filePaths.Length} файлов");
                combinedData.AppendLine("=".PadRight(50, '='));
                
                // Подготавливаем данные для отправки
                string combinedDataStr = combinedData.ToString();
                
                // Очищаем от не-ASCII символов перед отправкой
                string cleanDataForAi = CleanForAi(combinedDataStr);
                
                Console.WriteLine($"Отправляем данные в ИИ (размер: {cleanDataForAi.Length} символов)");
                
                // Показываем подготовленные данные
                Dispatcher.Invoke(() =>
                {
                    MarkdownViewer.Markdown = $"## Подготовка данных завершена\n\n" +
                                             $"✅ Обработано файлов: {processedFiles}/{filePaths.Length}\n\n" +
                                             $"📊 Общий размер данных: {cleanDataForAi.Length} символов\n\n" +
                                             $"⏳ Отправляем данные на анализ ИИ...";
                });
                
                // Отправляем в ИИ с таймаутом
                var sendTask = anal.requestAnalytics(cleanDataForAi);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                
                var completedTask = await Task.WhenAny(sendTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("Таймаут при отправке данных в ИИ");
                }
                
                // Сохраняем объединенные данные для возможного экспорта
                _currentReport = combinedDataStr;
                
                Console.WriteLine("Данные успешно отправлены в ИИ");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Критическая ошибка при обработке файлов: {ex.Message}");
                
                Dispatcher.Invoke(() =>
                {
                    MarkdownViewer.Markdown = $"## ❌ Ошибка при обработке файлов\n\n" +
                                             $"Произошла ошибка: {ex.Message}";
                });
                
                MessageBox.Show($"Ошибка при обработке файлов: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private string ConvertJsonToReadableText(string jsonContent, string fileName)
        {
            try
            {
                // Пытаемся распарсить JSON
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                
                StringBuilder result = new StringBuilder();
                
                // Извлекаем основные данные
                var root = doc.RootElement;
                
                // Для файлов с эмоциями - форматируем специально
                if (fileName.Contains("emotion", StringComparison.OrdinalIgnoreCase))
                {
                    result.AppendLine("ДАННЫЕ АНАЛИЗА ЭМОЦИЙ:");
                    
                    if (root.TryGetProperty("timestamp", out JsonElement timestamp))
                        result.AppendLine($"Время записи: {timestamp}");
                    
                    if (root.TryGetProperty("duration", out JsonElement duration))
                        result.AppendLine($"Длительность: {duration} сек");
                    
                    if (root.TryGetProperty("emotions", out JsonElement emotions))
                    {
                        result.AppendLine("\nЭмоциональные показатели:");
                        result.AppendLine(FormatEmotions(emotions));
                    }
                    
                    if (root.TryGetProperty("average_intensity", out JsonElement intensity))
                        result.AppendLine($"Средняя интенсивность: {intensity}");
                    
                    return result.ToString();
                }
                
                // Общий случай - просто форматируем JSON
                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions 
                { 
                    Indented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                
                doc.WriteTo(writer);
                writer.Flush();
                
                string formattedJson = Encoding.UTF8.GetString(stream.ToArray());
                
                // Ограничиваем размер
                if (formattedJson.Length > 5000)
                {
                    return formattedJson.Substring(0, 5000) + "\n... (файл слишком большой, показана только часть)";
                }
                
                return formattedJson;
            }
            catch (JsonException)
            {
                // Если не JSON, очищаем текст
                return CleanText(jsonContent);
            }
        }
        
        private string FormatEmotions(JsonElement emotions)
        {
            StringBuilder sb = new StringBuilder();
            
            if (emotions.ValueKind == JsonValueKind.Object)
            {
                foreach (var emotion in emotions.EnumerateObject())
                {
                    sb.AppendLine($"  {emotion.Name}: {emotion.Value}");
                }
            }
            else if (emotions.ValueKind == JsonValueKind.Array)
            {
                int count = 0;
                foreach (var item in emotions.EnumerateArray())
                {
                    if (count++ > 20) // Ограничиваем количество
                    {
                        sb.AppendLine("  ... (и еще записи)");
                        break;
                    }
                    
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        if (item.TryGetProperty("time", out JsonElement time))
                            sb.Append($"  Время {time}: ");
                        
                        if (item.TryGetProperty("emotion", out JsonElement emotion))
                            sb.AppendLine($"{emotion}");
                    }
                }
            }
            
            return sb.ToString();
        }
        
        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // Удаляем все не-ASCII символы
            StringBuilder clean = new StringBuilder();
            foreach (char c in text)
            {
                if (c < 128) // Только ASCII символы
                {
                    clean.Append(c);
                }
            }
            
            // Ограничиваем размер
            string result = clean.ToString();
            if (result.Length > 2000)
            {
                result = result.Substring(0, 2000) + "\n... (текст слишком большой, показана только часть)";
            }
            
            return result;
        }
        
        private string CleanForAi(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // Удаляем эмодзи и специальные символы
            StringBuilder clean = new StringBuilder();
            foreach (char c in text)
            {
                // Оставляем только печатаемые ASCII символы и русские буквы
                if ((c >= 32 && c <= 126) || // ASCII печатаемые
                    (c >= 'А' && c <= 'я') || // Русские буквы
                    c == 'ё' || c == 'Ё' || // Буква ё
                    c == '\n' || c == '\r' || c == '\t' || // Управляющие символы
                    c == ' ' || c == '.' || c == ',' || c == ':' || c == ';' || // Знаки препинания
                    c == '!' || c == '?' || c == '-' || c == '_' || c == '(' || c == ')')
                {
                    clean.Append(c);
                }
            }
            
            return clean.ToString();
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
            netReport.setMarkdown(_currentReport);
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
                Task.Run(async () =>
                {
                    try
                    {
                        await anal.requestAnalytics(RealTimeDataManager.Instance.GetCompactJson());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при запросе анализа: {ex.Message}");
                    }
                });
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
                        
                        System.IO.File.WriteAllText(saveDialog.FileName, fullReport, Encoding.UTF8);
                        
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