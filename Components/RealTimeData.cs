using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EmotionAnalyzer.Components
{
    // Класс для хранения данных в реальном времени
    public class RealTimeDataPoint : INotifyPropertyChanged
    {
        private double _value;
        private DateTime _timestamp;
        private TimeSpan _videoTime;

        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan VideoTime
        {
            get => _videoTime;
            set
            {
                _videoTime = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Менеджер данных в реальном времени
    public class RealTimeDataManager
    {
        private static RealTimeDataManager? _instance;
        public static RealTimeDataManager Instance => _instance ??= new RealTimeDataManager();

        // Коллекции данных для графиков
        public ObservableCollection<RealTimeDataPoint> AttentionData { get; } = new();
        public ObservableCollection<RealTimeDataPoint> RelaxationData { get; } = new();
        public ObservableCollection<RealTimeDataPoint> AlphaData { get; } = new();
        public ObservableCollection<RealTimeDataPoint> BetaData { get; } = new();
        public ObservableCollection<RealTimeDataPoint> GammaData { get; } = new();
        public ObservableCollection<RealTimeDataPoint> ThetaData { get; } = new();
        public ObservableCollection<RealTimeDataPoint> DeltaData { get; } = new();

        // Метод для добавления данных BrainBit
        public void AddBrainBitData(BrainBitManager.AnalysisData data)
        {
            // Ограничиваем количество точек на графике (последние 100)
            const int maxPoints = 100;

            // Внимание
            AttentionData.Add(new RealTimeDataPoint
            {
                Value = data.InstAttention,
                Timestamp = data.Timestamp,
                VideoTime = data.VideoTime
            });
            if (AttentionData.Count > maxPoints) AttentionData.RemoveAt(0);

            // Расслабление
            RelaxationData.Add(new RealTimeDataPoint
            {
                Value = data.InstRelaxation,
                Timestamp = data.Timestamp,
                VideoTime = data.VideoTime
            });
            if (RelaxationData.Count > maxPoints) RelaxationData.RemoveAt(0);

            // Альфа волны
            AlphaData.Add(new RealTimeDataPoint
            {
                Value = data.Alpha,
                Timestamp = data.Timestamp,
                VideoTime = data.VideoTime
            });
            if (AlphaData.Count > maxPoints) AlphaData.RemoveAt(0);

            // Бета волны
            BetaData.Add(new RealTimeDataPoint
            {
                Value = data.Beta,
                Timestamp = data.Timestamp,
                VideoTime = data.VideoTime
            });
            if (BetaData.Count > maxPoints) BetaData.RemoveAt(0);

            // Гамма волны
            GammaData.Add(new RealTimeDataPoint
            {
                Value = data.Gamma,
                Timestamp = data.Timestamp,
                VideoTime = data.VideoTime
            });
            if (GammaData.Count > maxPoints) GammaData.RemoveAt(0);

            // Тета волны
            ThetaData.Add(new RealTimeDataPoint
            {
                Value = data.Theta,
                Timestamp = data.Timestamp,
                VideoTime = data.VideoTime
            });
            if (ThetaData.Count > maxPoints) ThetaData.RemoveAt(0);

            // Дельта волны
            DeltaData.Add(new RealTimeDataPoint
            {
                Value = data.Delta,
                Timestamp = data.Timestamp,
                VideoTime = data.VideoTime
            });
            if (DeltaData.Count > maxPoints) DeltaData.RemoveAt(0);
        }

        // Очистка данных
        public void ClearAllData()
        {
            AttentionData.Clear();
            RelaxationData.Clear();
            AlphaData.Clear();
            BetaData.Clear();
            GammaData.Clear();
            ThetaData.Clear();
            DeltaData.Clear();
        }

        // Методы для получения статистики
        public double GetAverageAttention() => AttentionData.Count > 0 ? AttentionData.Average(d => d.Value) : 0;
        public double GetMaxAttention() => AttentionData.Count > 0 ? AttentionData.Max(d => d.Value) : 0;
        public double GetMinAttention() => AttentionData.Count > 0 ? AttentionData.Min(d => d.Value) : 0;

        public double GetAverageRelaxation() => RelaxationData.Count > 0 ? RelaxationData.Average(d => d.Value) : 0;
        public double GetMaxRelaxation() => RelaxationData.Count > 0 ? RelaxationData.Max(d => d.Value) : 0;
        public double GetMinRelaxation() => RelaxationData.Count > 0 ? RelaxationData.Min(d => d.Value) : 0;
    }
}