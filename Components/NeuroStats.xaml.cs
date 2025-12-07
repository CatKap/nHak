using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using Markdig;
using Markdig.Wpf;

namespace ScenaryBuilder.Components
{
    public partial class NeuroStats : Window
    {
        private static NeuroStats _instance;
        private static readonly object _lock = new object();
        private static Thread _uiThread;

        public static NeuroStats Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            // Проверяем, находимся ли мы в UI-потоке
                            if (System.Threading.Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                            {
                                // Создаем новый STA-поток для UI
                                _uiThread = new Thread(() =>
                                {
                                    Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
                                    _instance = new NeuroStats();
                                
                                    // Настраиваем окно
                                    _instance.Closed += (s, e) => _instance.Dispatcher.InvokeShutdown();
                                
                                    // Создаем новый диспетчер
                                    System.Windows.Threading.Dispatcher.Run();
                                });
                            
                                _uiThread.SetApartmentState(ApartmentState.STA);
                                _uiThread.Start();
                                _uiThread.Join(); // или используйте другие способы синхронизации
                            }
                            else
                            {
                                // Мы уже в STA-потоке
                                _instance = new NeuroStats();
                            }
                        }
                    }
                }
                return _instance;
            }
        }

        public NeuroStats()
        {
            InitializeComponent();
            // Инициализация окна
        }

        private class StatItem
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        

        private void InitializeStats()
        {
            var stats = new List<StatItem>
            {
                new StatItem { Name = "Active Neurons", Value = "1,234,567" },
                new StatItem { Name = "Connections", Value = "45.6M" },
                new StatItem { Name = "Accuracy", Value = "94.7%" },
                new StatItem { Name = "Training Loss", Value = "0.0234" },
                new StatItem { Name = "Memory Usage", Value = "2.3 GB" },
                new StatItem { Name = "Processing Speed", Value = "1.2 TFLOPS" }
            };

            StatsList.ItemsSource = stats;
        }



        public void setMarkdown(string markdown)
        {
            MarkdownViewer.Markdown = markdown;
        }


        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Экспорт в HTML
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .Build();

                string html = Markdig.Markdown.ToHtml(MarkdownViewer.Markdown, pipeline);

                // Здесь можно добавить логику сохранения файла
                MessageBox.Show("Export functionality would save HTML report here.",
                    "Export", MessageBoxButton.OK, MessageBoxImage.Information);


            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}