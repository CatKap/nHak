using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EmotionAnalyzer.Components
{
    public partial class CalibrationWindow : UserControl
    {
        private DispatcherTimer _timer;
        private int _remainingSeconds = 20;
        
        public event EventHandler CalibrationCompleted;
        public event EventHandler CalibrationCancelled;
        
        public CalibrationWindow()
        {
            InitializeComponent();
            InitializeTimer();
        }
        
        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
        }
        
        public void StartCalibration()
        {
            _remainingSeconds = 20;
            TimerText.Text = $"{_remainingSeconds} секунд";
            ProgressFill.Width = 0;
            _timer.Start();
        }
        
        private void Timer_Tick(object sender, EventArgs e)
        {
            _remainingSeconds--;
            
            // Обновляем текст таймера
            TimerText.Text = $"{_remainingSeconds} секунд";
            
            // Обновляем прогресс-бар
            double progress = ((20 - _remainingSeconds) / 20.0) * 300;
            ProgressFill.Width = progress;
            
            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                CalibrationCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            CalibrationCancelled?.Invoke(this, EventArgs.Empty);
        }
        
        public void StopCalibration()
        {
            if (_timer != null)
                _timer.Stop();
        }
    }
}