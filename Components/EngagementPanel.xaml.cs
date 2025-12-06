using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;

namespace EmotionAnalyzer.Components
{
    public partial class EngagementPanel : UserControl, INotifyPropertyChanged
    {
        private SeriesCollection _attentionSeries;
        private SeriesCollection _relaxationSeries;
        private Func<double, string> _xAxisFormatter;
        private double _currentAttention;
        private double _currentRelaxation;
        private string _engagementStatus;

        public EngagementPanel()
        {
            InitializeComponent();
            
            // Инициализируем данные
            InitializeCharts();
            
            // Устанавливаем контекст данных
            DataContext = this;
        }

        private void InitializeCharts()
        {
            // Инициализируем серии данных
            AttentionSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Внимание",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.DodgerBlue,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                }
            };

            RelaxationSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Расслабление",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.LimeGreen,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                }
            };

            // Форматтер для оси X
            XAxisFormatter = value => $"{value:F0} с";
            
            // Начальный статус
            EngagementStatus = "Ожидание данных BrainBit";
            CurrentAttentionText.Text = "Внимание: 0.00";
        }

        // Свойства для привязки данных
        public SeriesCollection AttentionSeries
        {
            get => _attentionSeries;
            set
            {
                _attentionSeries = value;
                OnPropertyChanged();
            }
        }

        public SeriesCollection RelaxationSeries
        {
            get => _relaxationSeries;
            set
            {
                _relaxationSeries = value;
                OnPropertyChanged();
            }
        }

        public Func<double, string> XAxisFormatter
        {
            get => _xAxisFormatter;
            set
            {
                _xAxisFormatter = value;
                OnPropertyChanged();
            }
        }

        public string EngagementStatus
        {
            get => _engagementStatus;
            set
            {
                _engagementStatus = value;
                OnPropertyChanged();
                if (EngagementStatusText != null)
                    EngagementStatusText.Text = value;
            }
        }

        public double CurrentAttention
        {
            get => _currentAttention;
            set
            {
                _currentAttention = value;
                OnPropertyChanged();
                UpdateAttentionText();
            }
        }

        public double CurrentRelaxation
        {
            get => _currentRelaxation;
            set
            {
                _currentRelaxation = value;
                OnPropertyChanged();
            }
        }

        // Метод для обновления данных из BrainBit
        public void UpdateEngagementData(double attention, double relativeAttention, TimeSpan videoTime)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Обновляем текущие значения
                    CurrentAttention = attention;
                    CurrentRelaxation = RealTimeDataManager.Instance.GetAverageRelaxation();
                    
                    // Добавляем данные в графики
                    if (AttentionSeries[0].Values.Count > 100)
                        AttentionSeries[0].Values.RemoveAt(0);
                    AttentionSeries[0].Values.Add(attention);
                    
                    if (RelaxationSeries[0].Values.Count > 100)
                        RelaxationSeries[0].Values.RemoveAt(0);
                    RelaxationSeries[0].Values.Add(CurrentRelaxation);
                    
                    // Обновляем статус вовлеченности
                    UpdateEngagementStatus(attention, relativeAttention);
                    
                    // Обновляем текст текущего внимания
                    UpdateAttentionText();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обновления данных Engagement: {ex.Message}");
                }
            });
        }

        private void UpdateEngagementStatus(double attention, double relativeAttention)
        {
            string status;
            Color color;
            
            if (attention >= 0.8)
            {
                status = "Очень вовлечён";
                color = Colors.DodgerBlue;
            }
            else if (attention >= 0.6)
            {
                status = "Высоко вовлечён";
                color = Colors.RoyalBlue;
            }
            else if (attention >= 0.4)
            {
                status = "Средне вовлечён";
                color = Colors.SteelBlue;
            }
            else if (attention >= 0.2)
            {
                status = "Слабо вовлечён";
                color = Colors.LightSlateGray;
            }
            else
            {
                status = "Не вовлечён";
                color = Colors.Gray;
            }
            
            EngagementStatus = status;
            if (EngagementStatusText != null)
                EngagementStatusText.Foreground = new SolidColorBrush(color);
        }

        private void UpdateAttentionText()
        {
            CurrentAttentionText.Text = $"Внимание: {CurrentAttention:F2}";
            
            // Меняем цвет в зависимости от значения
            if (CurrentAttentionText != null)
            {
                var color = CurrentAttention >= 0.5 
                    ? Colors.DodgerBlue 
                    : Colors.Gray;
                CurrentAttentionText.Foreground = new SolidColorBrush(color);
            }
        }

        // Обработчики кнопок временных интервалов
        private void TimeIntervalFull_Click(object sender, RoutedEventArgs e)
        {
            UpdateActiveTimeInterval(TimeIntervalFull);
            // Здесь можно добавить логику фильтрации данных по интервалу
        }

        private void TimeInterval1Min_Click(object sender, RoutedEventArgs e)
        {
            UpdateActiveTimeInterval(TimeInterval1Min);
        }

        private void TimeInterval5Min_Click(object sender, RoutedEventArgs e)
        {
            UpdateActiveTimeInterval(TimeInterval5Min);
        }

        private void TimeInterval10Min_Click(object sender, RoutedEventArgs e)
        {
            UpdateActiveTimeInterval(TimeInterval10Min);
        }

        private void UpdateActiveTimeInterval(Button activeButton)
        {
            // Сброс цветов всех кнопок
            var buttons = new[] { TimeIntervalFull, TimeInterval1Min, TimeInterval5Min, TimeInterval10Min };
            foreach (var button in buttons)
            {
                button.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                button.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            }
            
            // Установка активной кнопки
            activeButton.Background = new SolidColorBrush(Color.FromRgb(30, 144, 255));
            activeButton.Foreground = Brushes.White;
        }

        // Очистка данных
        public void ClearCharts()
        {
            AttentionSeries[0].Values.Clear();
            RelaxationSeries[0].Values.Clear();
            EngagementStatus = "Ожидание данных BrainBit";
            CurrentAttention = 0;
            CurrentRelaxation = 0;
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}