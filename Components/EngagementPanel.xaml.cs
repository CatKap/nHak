using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Wpf;

namespace EmotionAnalyzer.Components
{
    public partial class EngagementPanel : UserControl
    {
        private ChartValues<double> _betaValues;
        private ChartValues<double> _alphaValues;
        private DispatcherTimer? _updateTimer;
        private int _timeCounter = 0;
        private double _betaMin = 0, _betaMax = 100, _betaAvg = 0;
        private double _alphaMin = 0, _alphaMax = 100, _alphaAvg = 0;
        
        public EngagementPanel()
        {
            InitializeComponent();
            InitializeCharts();
            UpdateStatsText();
        }

        private void InitializeCharts()
        {
            // Инициализируем коллекции значений
            _betaValues = new ChartValues<double>();
            _alphaValues = new ChartValues<double>();
            
            // Настройка графика бета-волн
            BetaChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Бета-волны",
                    Values = _betaValues,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 4,
                    LineSmoothness = 0.3, // Легкое сглаживание
                    Stroke = Brushes.IndianRed,
                    StrokeThickness = 3,
                    Fill = new LinearGradientBrush
                    {
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(100, 255, 107, 107), 0),
                            new GradientStop(Colors.Transparent, 1)
                        }
                    }
                }
            };
            
            // Настройка графика альфа-волн
            AlphaChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Альфа-волны",
                    Values = _alphaValues,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 4,
                    LineSmoothness = 0.3, // Легкое сглаживание
                    Stroke = Brushes.MediumTurquoise,
                    StrokeThickness = 3,
                    Fill = new LinearGradientBrush
                    {
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(100, 78, 205, 196), 0),
                            new GradientStop(Colors.Transparent, 1)
                        }
                    }
                }
            };
            
            // Настройка осей для бета-графика
            ConfigureChartAxes(BetaChart, "Бета-волны (β)");
            ConfigureChartAxes(AlphaChart, "Альфа-волны (α)");
            
            // Включаем анимации
            BetaChart.DisableAnimations = false;
            AlphaChart.DisableAnimations = false;
            BetaChart.AnimationsSpeed = TimeSpan.FromSeconds(0.5);
            AlphaChart.AnimationsSpeed = TimeSpan.FromSeconds(0.5);
            
            // Начальные значения
            EngagementStatusText.Text = "📡 Ожидание данных BrainBit";
            CurrentBetaText.Text = "Бета: 0.00%";
            CurrentAlphaText.Text = "Альфа: 0.00%";
            
            // Цвета для текстовых полей
            CurrentBetaText.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            CurrentAlphaText.Foreground = new SolidColorBrush(Color.FromRgb(78, 205, 196));
        }

        private void ConfigureChartAxes(CartesianChart chart, string title)
        {
            chart.AxisX.Clear();
            chart.AxisX.Add(new Axis
            {
                Title = "Время (сек)",
                LabelFormatter = value => $"{value:F0} с",
                Foreground = Brushes.DimGray,
                FontSize = 10,
                Separator = new LiveCharts.Wpf.Separator
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 189, 195, 199)),
                    StrokeThickness = 1
                }
            });
            
            chart.AxisY.Clear();
            chart.AxisY.Add(new Axis
            {
                Title = "Процент %",
                MinValue = 0,
                MaxValue = 100,
                Foreground = Brushes.DimGray,
                FontSize = 10,
                LabelFormatter = value => $"{value:F0}%",
                Separator = new LiveCharts.Wpf.Separator
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 189, 195, 199)),
                    StrokeThickness = 1
                }
            });
        }

        // Метод для обновления данных ЭЭГ
        public void UpdateEegData(double alpha, double beta, double gamma, double theta, double delta, TimeSpan videoTime)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Нормализуем значения
                    beta = Math.Max(0, Math.Min(100, beta));
                    alpha = Math.Max(0, Math.Min(100, alpha));
                    
                    // Обновляем текстовые поля
                    CurrentBetaText.Text = $"Бета: {beta:F1}%";
                    CurrentAlphaText.Text = $"Альфа: {alpha:F1}%";
                    
                    // Добавляем данные в графики
                    AddDataPoint(beta, alpha);
                    
                    // Обновляем статистику
                    UpdateStatistics(beta, alpha);
                    
                    // Обновляем статус на основе соотношения бета/альфа
                    UpdateStatusBasedOnBetaAlphaRatio(beta, alpha);
                    
                    // Динамически подстраиваем масштаб
                    AutoAdjustYAxis();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обновления данных ЭЭГ: {ex.Message}");
                }
            });
        }

        private void AddDataPoint(double beta, double alpha)
        {
            // Добавляем новые точки
            _betaValues.Add(beta);
            _alphaValues.Add(alpha);
            
            // Ограничиваем количество точек на графике
            const int maxPoints = 180; // 3 минуты при обновлении раз в секунду
            if (_betaValues.Count > maxPoints)
            {
                _betaValues.RemoveAt(0);
                _alphaValues.RemoveAt(0);
            }
            
            _timeCounter++;
        }

        private void UpdateStatistics(double beta, double alpha)
        {
            // Обновляем статистику
            if (_betaValues.Count > 0)
            {
                _betaMin = _betaValues.Min();
                _betaMax = _betaValues.Max();
                _betaAvg = _betaValues.Average();
                
                _alphaMin = _alphaValues.Min();
                _alphaMax = _alphaValues.Max();
                _alphaAvg = _alphaValues.Average();
                
                UpdateStatsText();
            }
        }

        private void UpdateStatsText()
        {
            BetaStatsText.Text = $"Мин: {_betaMin:F1}  Ср: {_betaAvg:F1}  Макс: {_betaMax:F1}";
            AlphaStatsText.Text = $"Мин: {_alphaMin:F1}  Ср: {_alphaAvg:F1}  Макс: {_alphaMax:F1}";
        }

        private void AutoAdjustYAxis()
        {
            // Автоматическая подстройка масштаба Y оси для лучшей наглядности
            if (_betaValues.Count > 10)
            {
                double betaRange = _betaMax - _betaMin;
                double alphaRange = _alphaMax - _alphaMin;
                
                // Добавляем 10% запаса сверху и снизу
                double betaPadding = betaRange * 0.1;
                double alphaPadding = alphaRange * 0.1;
                
                // Устанавливаем новые границы, но не меньше 20%
                BetaChart.AxisY[0].MinValue = Math.Max(0, _betaMin - betaPadding);
                BetaChart.AxisY[0].MaxValue = Math.Min(100, _betaMax + betaPadding);
                
                AlphaChart.AxisY[0].MinValue = Math.Max(0, _alphaMin - alphaPadding);
                AlphaChart.AxisY[0].MaxValue = Math.Min(100, _alphaMax + alphaPadding);
            }
        }

        private void UpdateStatusBasedOnBetaAlphaRatio(double beta, double alpha)
        {
            string status;
            Color color;
            string emoji = "";
            
            if (alpha <= 0.1) alpha = 0.1; // Избегаем деления на ноль
            
            double betaAlphaRatio = beta / alpha;
            
            if (betaAlphaRatio > 2.5)
            {
                status = "Интенсивная умственная деятельность";
                color = Color.FromRgb(231, 76, 60); // Красный
                emoji = "🔥";
            }
            else if (betaAlphaRatio > 2.0)
            {
                status = "Высокая концентрация";
                color = Color.FromRgb(230, 126, 34); // Оранжевый
                emoji = "🎯";
            }
            else if (betaAlphaRatio > 1.5)
            {
                status = "Активное мышление";
                color = Color.FromRgb(241, 196, 15); // Желтый
                emoji = "💡";
            }
            else if (betaAlphaRatio > 1.0)
            {
                status = "Сбалансированное состояние";
                color = Color.FromRgb(46, 204, 113); // Зеленый
                emoji = "⚖️";
            }
            else if (betaAlphaRatio > 0.7)
            {
                status = "Спокойное сосредоточение";
                color = Color.FromRgb(52, 152, 219); // Синий
                emoji = "🧘";
            }
            else if (betaAlphaRatio > 0.4)
            {
                status = "Расслабленное состояние";
                color = Color.FromRgb(155, 89, 182); // Фиолетовый
                emoji = "😌";
            }
            else
            {
                status = "Глубокое расслабление/Медитация";
                color = Color.FromRgb(52, 73, 94); // Темно-синий
                emoji = "🕊️";
            }
            
            EngagementStatusText.Text = $"{emoji} {status}";
            EngagementStatusText.Foreground = new SolidColorBrush(color);
        }

        // Обработчики кнопок временных интервалов
        private void TimeIntervalFull_Click(object sender, RoutedEventArgs e)
        {
            UpdateActiveTimeInterval(TimeIntervalFull);
            SetTimeRange(0);
            BetaChart.AxisY[0].MinValue = 0;
            BetaChart.AxisY[0].MaxValue = 100;
            AlphaChart.AxisY[0].MinValue = 0;
            AlphaChart.AxisY[0].MaxValue = 100;
        }

        private void TimeInterval1Min_Click(object sender, RoutedEventArgs e)
        {
            UpdateActiveTimeInterval(TimeInterval1Min);
            SetTimeRange(60);
        }

        private void TimeInterval5Min_Click(object sender, RoutedEventArgs e)
        {
            UpdateActiveTimeInterval(TimeInterval5Min);
            SetTimeRange(300);
        }

        private void TimeInterval10Min_Click(object sender, RoutedEventArgs e)
        {
            UpdateActiveTimeInterval(TimeInterval10Min);
            SetTimeRange(600);
        }

        private void UpdateActiveTimeInterval(Button activeButton)
        {
            var buttons = new[] { TimeIntervalFull, TimeInterval1Min, TimeInterval5Min, TimeInterval10Min };
            foreach (var button in buttons)
            {
                button.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                button.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            }
            
            activeButton.Background = new SolidColorBrush(Color.FromRgb(30, 144, 255));
            activeButton.Foreground = Brushes.White;
        }

        private void SetTimeRange(int seconds)
        {
            if (seconds == 0)
            {
                BetaChart.AxisX[0].MinValue = double.NaN;
                BetaChart.AxisX[0].MaxValue = double.NaN;
                AlphaChart.AxisX[0].MinValue = double.NaN;
                AlphaChart.AxisX[0].MaxValue = double.NaN;
            }
            else if (_betaValues.Count > 0)
            {
                int totalPoints = _betaValues.Count;
                int pointsToShow = Math.Min(seconds, totalPoints);
                
                BetaChart.AxisX[0].MinValue = totalPoints - pointsToShow;
                BetaChart.AxisX[0].MaxValue = totalPoints;
                AlphaChart.AxisX[0].MinValue = totalPoints - pointsToShow;
                AlphaChart.AxisX[0].MaxValue = totalPoints;
            }
        }

        // Метод для тестирования с более динамичными данными
        public void AddTestData()
        {
            Random rnd = new Random();
            
            _betaValues.Clear();
            _alphaValues.Clear();
            
            // Генерируем более наглядные тестовые данные
            for (int i = 0; i < 120; i++)
            {
                // Создаем более заметные изменения
                double beta = 30 + 25 * Math.Sin(i * 0.15) + 
                             10 * Math.Sin(i * 0.05) + 
                             (i % 40 == 0 ? 15 : 0) + // Резкие всплески
                             rnd.NextDouble() * 3;
                
                double alpha = 40 + 20 * Math.Cos(i * 0.12) + 
                              8 * Math.Cos(i * 0.03) + 
                              (i % 35 == 0 ? -12 : 0) + // Резкие падения
                              rnd.NextDouble() * 3;
                
                _betaValues.Add(Math.Max(0, Math.Min(100, beta)));
                _alphaValues.Add(Math.Max(0, Math.Min(100, alpha)));
            }
            
            UpdateStatistics(_betaValues.Last(), _alphaValues.Last());
            UpdateStatusBasedOnBetaAlphaRatio(_betaValues.Last(), _alphaValues.Last());
            
            // Автоматически подстраиваем масштаб
            AutoAdjustYAxis();
        }

        // Метод для очистки данных
        public void ClearCharts()
        {
            _betaValues?.Clear();
            _alphaValues?.Clear();
            
            EngagementStatusText.Text = "📡 Ожидание данных BrainBit";
            EngagementStatusText.Foreground = new SolidColorBrush(Color.FromRgb(30, 144, 255));
            
            CurrentBetaText.Text = "Бета: 0.00%";
            CurrentAlphaText.Text = "Альфа: 0.00%";
            
            BetaStatsText.Text = "Мин: 0.0  Ср: 0.0  Макс: 0.0";
            AlphaStatsText.Text = "Мин: 0.0  Ср: 0.0  Макс: 0.0";
            
            // Сбрасываем масштаб
            BetaChart.AxisY[0].MinValue = 0;
            BetaChart.AxisY[0].MaxValue = 100;
            AlphaChart.AxisY[0].MinValue = 0;
            AlphaChart.AxisY[0].MaxValue = 100;
        }

        // Метод для улучшенного тестирования
        public void StartEnhancedTestUpdates()
        {
            if (_updateTimer != null) return;
            
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.5) // Более частое обновление
            };
            
            int patternCounter = 0;
            
            _updateTimer.Tick += (s, e) =>
            {
                Random rnd = new Random();
                
                // Создаем более сложные и заметные паттерны
                patternCounter++;
                
                // Паттерны для бета-волн
                double betaBase = 30 + 10 * Math.Sin(patternCounter * 0.08);
                double betaPattern = 15 * Math.Sin(patternCounter * 0.15);
                double betaSpike = (patternCounter % 25 == 0) ? 25 : 0;
                double betaNoise = rnd.NextDouble() * 4;
                
                // Паттерны для альфа-волн
                double alphaBase = 40 + 8 * Math.Cos(patternCounter * 0.07);
                double alphaPattern = 12 * Math.Cos(patternCounter * 0.12);
                double alphaDrop = (patternCounter % 30 == 0) ? -20 : 0;
                double alphaNoise = rnd.NextDouble() * 4;
                
                double beta = betaBase + betaPattern + betaSpike + betaNoise;
                double alpha = alphaBase + alphaPattern + alphaDrop + alphaNoise;
                
                UpdateEegData(
                    Math.Max(5, Math.Min(95, alpha)),
                    Math.Max(5, Math.Min(95, beta)),
                    10 + rnd.NextDouble() * 10, // Gamma
                    5 + rnd.NextDouble() * 8,   // Theta
                    3 + rnd.NextDouble() * 6,   // Delta
                    TimeSpan.FromSeconds(patternCounter * 0.5));
            };
            
            _updateTimer.Start();
        }

        public void StopTestUpdates()
        {
            _updateTimer?.Stop();
            _updateTimer = null;
        }
    }
}