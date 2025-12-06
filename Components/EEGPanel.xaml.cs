using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Definitions.Series;
using LiveCharts.Wpf;

namespace EmotionAnalyzer.Components
{
    public partial class EEGPanel : UserControl, INotifyPropertyChanged
    {
        private SeriesCollection _alphaBetaSeries;
        private SeriesCollection _gammaThetaDeltaSeries;
        private Func<double, string> _xAxisFormatter;
        private double _currentAlpha;
        private double _currentBeta;
        private double _currentGamma;
        private double _currentTheta;
        private double _currentDelta;

        public EEGPanel()
        {
            InitializeComponent();
            
            // Инициализируем графики
            InitializeCharts();
            
            // Устанавливаем контекст данных
            DataContext = this;
        }

        private void InitializeCharts()
        {
            // Инициализируем серии для Альфа и Бета
            AlphaBetaSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Альфа (α)",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.RoyalBlue,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                },
                new LineSeries
                {
                    Title = "Бета (β)",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.LimeGreen,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                }
            };

            // Инициализируем серии для Гамма, Тета и Дельта
            GammaThetaDeltaSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Гамма (γ)",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.OrangeRed,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                },
                new LineSeries
                {
                    Title = "Тета (θ)",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.BlueViolet,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                },
                new LineSeries
                {
                    Title = "Дельта (δ)",
                    Values = new ChartValues<double>(),
                    PointGeometry = null,
                    LineSmoothness = 0,
                    Stroke = Brushes.SteelBlue,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                }
            };

            // Форматтер для оси X
            XAxisFormatter = value => $"{value:F0} с";
            
            // Начальные значения
            UpdateWaveValues(0, 0, 0, 0, 0);
        }

        // Свойства для привязки данных
        public SeriesCollection AlphaBetaSeries
        {
            get => _alphaBetaSeries;
            set
            {
                _alphaBetaSeries = value;
                OnPropertyChanged();
            }
        }

        public SeriesCollection GammaThetaDeltaSeries
        {
            get => _gammaThetaDeltaSeries;
            set
            {
                _gammaThetaDeltaSeries = value;
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

        // Метод для обновления данных из BrainBit
        public void UpdateEEGData(double alpha, double beta, double gamma, double theta, double delta,
                                  double attention, double relaxation)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Обновляем текущие значения
                    UpdateWaveValues(alpha, beta, gamma, theta, delta);
                    
                    // Добавляем данные в графики
                    AddDataToChart(AlphaBetaSeries[0], alpha, 100);
                    AddDataToChart(AlphaBetaSeries[1], beta, 100);
                    AddDataToChart(GammaThetaDeltaSeries[0], gamma, 100);
                    AddDataToChart(GammaThetaDeltaSeries[1], theta, 100);
                    AddDataToChart(GammaThetaDeltaSeries[2], delta, 100);
                    
                    // Обновляем статус Альфа/Бета
                    UpdateAlphaBetaStatus(alpha, beta);
                    
                    // Обновляем индикатор эмоций
                    UpdateEmotionIndicator(alpha, beta, attention, relaxation);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обновления данных EEG: {ex.Message}");
                }
            });
        }

        private void AddDataToChart(ISeriesView series, double value, int maxPoints)
        {
            if (series.Values.Count > maxPoints)
                series.Values.RemoveAt(0);
            series.Values.Add(value);
        }

        private void UpdateWaveValues(double alpha, double beta, double gamma, double theta, double delta)
        {
            _currentAlpha = alpha;
            _currentBeta = beta;
            _currentGamma = gamma;
            _currentTheta = theta;
            _currentDelta = delta;

            // Обновляем текстовые значения
            if (AlphaValueText != null)
                AlphaValueText.Text = $"α: {alpha:F1}%";
            if (BetaValueText != null)
                BetaValueText.Text = $"β: {beta:F1}%";
            if (GammaValueText != null)
                GammaValueText.Text = $"γ: {gamma:F1}%";
            if (ThetaValueText != null)
                ThetaValueText.Text = $"θ: {theta:F1}%";
            if (DeltaValueText != null)
                DeltaValueText.Text = $"δ: {delta:F1}%";
        }

        private void UpdateAlphaBetaStatus(double alpha, double beta)
        {
            if (AlphaBetaStatus == null) return;

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
            if (JoyIndicator == null) return;

            string emotion;
            Color color;

            // Логика определения эмоции на основе спектральных волн
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
        

        public void UpdateJoyIndicator(string emotion)
        {
            if (JoyIndicator != null)
                JoyIndicator.Text = emotion;
        }

        // Очистка графиков
        public void ClearCharts()
        {
            foreach (var series in AlphaBetaSeries)
                series.Values.Clear();
            
            foreach (var series in GammaThetaDeltaSeries)
                series.Values.Clear();
            
            UpdateWaveValues(0, 0, 0, 0, 0);
            
            if (JoyIndicator != null)
            {
                JoyIndicator.Text = "Нейтральное состояние";
                JoyIndicator.Foreground = new SolidColorBrush(Colors.SteelBlue);
            }
            
            if (AlphaBetaStatus != null)
                AlphaBetaStatus.Text = "Альфа: Расслабление | Бета: Активность";
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}