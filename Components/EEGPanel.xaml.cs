using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Wpf;
using System.Linq;

namespace EmotionAnalyzer.Components
{
    public partial class EEGPanel : UserControl
    {
        // Коллекции значений для графиков
        private ChartValues<double> _alphaValues;
        private ChartValues<double> _betaValues;
        private ChartValues<double> _gammaValues;
        private ChartValues<double> _thetaValues;
        private ChartValues<double> _deltaValues;
        
        // Таймер для тестирования
        private DispatcherTimer? _testTimer;
        private Random _random;
        private int _timeCounter = 0;

        public EEGPanel()
        {
            InitializeComponent();
            _random = new Random();
            InitializeCharts();
        }

        private void InitializeCharts()
        {
            // Инициализируем коллекции значений
            _alphaValues = new ChartValues<double>();
            _betaValues = new ChartValues<double>();
            _gammaValues = new ChartValues<double>();
            _thetaValues = new ChartValues<double>();
            _deltaValues = new ChartValues<double>();
            
            // Настройка графика Альфа и Бета
            AlphaBetaChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Альфа (α)",
                    Values = _alphaValues,
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.RoyalBlue,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                },
                new LineSeries
                {
                    Title = "Бета (β)",
                    Values = _betaValues,
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.LimeGreen,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                }
            };
            
            // Настройка графика Гамма, Тета, Дельта
            GammaThetaDeltaChart.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Гамма (γ)",
                    Values = _gammaValues,
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.OrangeRed,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                },
                new LineSeries
                {
                    Title = "Тета (θ)",
                    Values = _thetaValues,
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.BlueViolet,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                },
                new LineSeries
                {
                    Title = "Дельта (δ)",
                    Values = _deltaValues,
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.SteelBlue,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                }
            };
            
            // Настройка осей для AlphaBetaChart
            ConfigureChartAxes(AlphaBetaChart, "Время (сек)", "% от общей мощности");
            
            // Настройка осей для GammaThetaDeltaChart
            ConfigureChartAxes(GammaThetaDeltaChart, "Время (сек)", "% от общей мощности");
            
            // Начальные значения
            UpdateWaveText(0, 0, 0, 0, 0);
            JoyIndicator.Text = "Нейтральное состояние";
            AlphaBetaStatus.Text = "Альфа: Расслабление | Бета: Активность";
            
            // Запускаем тестовые данные
            StartTestData();
        }
        
        private void ConfigureChartAxes(CartesianChart chart, string xTitle, string yTitle)
        {
            // Очищаем существующие оси
            chart.AxisX.Clear();
            chart.AxisY.Clear();
            
            // Добавляем ось X
            chart.AxisX.Add(new Axis
            {
                Title = xTitle,
                LabelFormatter = value => $"{value:F0} с",
                Separator = new LiveCharts.Wpf.Separator { Step = 10 }
            });
            
            // Добавляем ось Y
            chart.AxisY.Add(new Axis
            {
                Title = yTitle,
                MinValue = 0,
                MaxValue = 100,
                LabelFormatter = value => $"{value:F0}%"
            });
        }

        // Основной метод для обновления данных
        public void UpdateEEGData(double alpha, double beta, double gamma, double theta, double delta,
                                  double attention, double relaxation)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Обновляем текстовые значения
                    UpdateWaveText(alpha, beta, gamma, theta, delta);
                    
                    // Добавляем данные в графики
                    AddDataPoint(alpha, beta, gamma, theta, delta);
                    
                    // Обновляем статусы
                    UpdateAlphaBetaStatus(alpha, beta);
                    UpdateEmotionIndicator(alpha, beta, attention, relaxation);
                }
                catch (Exception ex)
                {
                }
            });
        }
        
        private void AddDataPoint(double alpha, double beta, double gamma, double theta, double delta)
        {
            // Добавляем новые значения
            _alphaValues.Add(alpha);
            _betaValues.Add(beta);
            _gammaValues.Add(gamma);
            _thetaValues.Add(theta);
            _deltaValues.Add(delta);
            
            // Ограничиваем количество точек (последние 120 секунд)
            const int maxPoints = 120;
            TrimChartValues(_alphaValues, maxPoints);
            TrimChartValues(_betaValues, maxPoints);
            TrimChartValues(_gammaValues, maxPoints);
            TrimChartValues(_thetaValues, maxPoints);
            TrimChartValues(_deltaValues, maxPoints);
            
            // Обновляем оси X
            UpdateXAxis();
            
            _timeCounter++;
        }
        
        private void TrimChartValues(ChartValues<double> values, int maxCount)
        {
            if (values.Count > maxCount)
            {
                values.RemoveAt(0);
            }
        }
        
        private void UpdateXAxis()
        {
            // Обновляем метки оси X для обоих графиков
            int count = _alphaValues.Count;
            
            AlphaBetaChart.AxisX[0].Labels.Clear();
            GammaThetaDeltaChart.AxisX[0].Labels.Clear();
            
            for (int i = 0; i < count; i++)
            {
                string label = (i % 10 == 0) ? $"{i}" : "";
                AlphaBetaChart.AxisX[0].Labels.Add(label);
                GammaThetaDeltaChart.AxisX[0].Labels.Add(label);
            }
        }
        
        private void UpdateWaveText(double alpha, double beta, double gamma, double theta, double delta)
        {
            AlphaValueText.Text = $"α: {alpha:F1}%";
            BetaValueText.Text = $"β: {beta:F1}%";
            GammaValueText.Text = $"γ: {gamma:F1}%";
            ThetaValueText.Text = $"θ: {theta:F1}%";
            DeltaValueText.Text = $"δ: {delta:F1}%";
        }
        
        private void UpdateAlphaBetaStatus(double alpha, double beta)
        {
            string alphaStatus = alpha > 30 ? "Высокое расслабление" :
                                 alpha > 20 ? "Среднее расслабление" :
                                 "Низкое расслабление";

            string betaStatus = beta > 20 ? "Высокая активность" :
                                beta > 10 ? "Средняя активность" :
                                "Низкая активность";

            AlphaBetaStatus.Text = $"Альфа: {alphaStatus} | Бета: {betaStatus}";
        }
        
        private void UpdateEmotionIndicator(double alpha, double beta, double attention, double relaxation)
        {
            string emotion;
            Color color;

            if (alpha > 35 && beta < 15)
            {
                emotion = "Глубокое расслабление";
                color = Colors.MediumPurple;
            }
            else if (beta > 25 && alpha < 20)
            {
                emotion = "Повышенная активность";
                color = Colors.OrangeRed;
            }
            else if (attention > 0.7 && beta > 20)
            {
                emotion = "Высокая концентрация";
                color = Colors.DodgerBlue;
            }
            else if (relaxation > 0.6 && alpha > 25)
            {
                emotion = "Расслабление";
                color = Colors.MediumSeaGreen;
            }
            else
            {
                emotion = "Творческое состояние";
                color = Colors.Orchid;
            }

            JoyIndicator.Text = emotion;
            JoyIndicator.Foreground = new SolidColorBrush(color);
        }
        
        // Метод для тестирования (генерирует тестовые данные)
        private void StartTestData()
        {
            if (_testTimer != null) return;
            
            _testTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.5)
            };
            
            _testTimer.Tick += (s, e) =>
            {
                // Генерируем тестовые данные
                double alpha = 25 + 15 * Math.Sin(_timeCounter * 0.05) + _random.NextDouble() * 3;
                double beta = 15 + 10 * Math.Sin(_timeCounter * 0.08 + 1) + _random.NextDouble() * 3;
                double gamma = 8 + 5 * Math.Sin(_timeCounter * 0.1 + 2) + _random.NextDouble() * 2;
                double theta = 12 + 8 * Math.Sin(_timeCounter * 0.07 + 3) + _random.NextDouble() * 2;
                double delta = 10 + 6 * Math.Sin(_timeCounter * 0.06 + 4) + _random.NextDouble() * 2;
                
                // Тестовые значения внимания и расслабления
                double attention = 0.5 + 0.3 * Math.Sin(_timeCounter * 0.03);
                double relaxation = 0.5 + 0.3 * Math.Cos(_timeCounter * 0.03);
                
                UpdateEEGData(
                    Math.Max(0, Math.Min(100, alpha)),
                    Math.Max(0, Math.Min(100, beta)),
                    Math.Max(0, Math.Min(100, gamma)),
                    Math.Max(0, Math.Min(100, theta)),
                    Math.Max(0, Math.Min(100, delta)),
                    attention,
                    relaxation);
            };
            
            _testTimer.Start();
        }
        
        // Метод для очистки графиков
        public void ClearCharts()
        {
            _alphaValues.Clear();
            _betaValues.Clear();
            _gammaValues.Clear();
            _thetaValues.Clear();
            _deltaValues.Clear();
            
            UpdateWaveText(0, 0, 0, 0, 0);
            JoyIndicator.Text = "Нейтральное состояние";
            JoyIndicator.Foreground = new SolidColorBrush(Colors.SteelBlue);
            AlphaBetaStatus.Text = "Альфа: Расслабление | Бета: Активность";
            
            _timeCounter = 0;
        }
        
        // Метод для остановки тестовых данных
        public void StopTestData()
        {
            _testTimer?.Stop();
            _testTimer = null;
        }
        
        // Метод для добавления тестовых данных (для отладки)
        public void AddTestData()
        {
            ClearCharts();
            
            // Добавляем 100 тестовых точек
            for (int i = 0; i < 100; i++)
            {
                double alpha = 25 + 15 * Math.Sin(i * 0.1);
                double beta = 15 + 10 * Math.Sin(i * 0.15);
                double gamma = 8 + 5 * Math.Sin(i * 0.2);
                double theta = 12 + 8 * Math.Sin(i * 0.18);
                double delta = 10 + 6 * Math.Sin(i * 0.12);
                
                _alphaValues.Add(Math.Max(0, Math.Min(100, alpha)));
                _betaValues.Add(Math.Max(0, Math.Min(100, beta)));
                _gammaValues.Add(Math.Max(0, Math.Min(100, gamma)));
                _thetaValues.Add(Math.Max(0, Math.Min(100, theta)));
                _deltaValues.Add(Math.Max(0, Math.Min(100, delta)));
            }
                
            UpdateXAxis();
        }
    }
}