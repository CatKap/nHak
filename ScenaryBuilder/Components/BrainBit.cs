using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NeuroSDK;
using SignalMath;

namespace EmotionAnalyzer
{
    public class BrainBitManager : IDisposable
    {
        // События
        public event Action<bool>? AnalysisStateChanged;
        public event Action<AnalysisData>? AnalysisDataReceived;
        
        // Компоненты
        private Scanner? _scanner;
        private BrainBitSensor? _sensor;
        private EegEmotionalMath? _math;
        private CancellationTokenSource? _cts;
        private Task? _analysisTask;
        
        // Настройки
        private const uint SamplingFrequency = 250;
        private bool _isCalibrated = false;
        private bool _isAnalyzing = false;
        private DateTime? _analysisStartTime;
        
        public bool IsAnalyzing => _isAnalyzing;
        public bool IsCalibrated => _isCalibrated;
        // УДАЛИЛ: public bool IsConnected => _sensor != null && _sensor.IsConnected; // ЭТОЙ СТРОКИ НЕТ
        
        public class AnalysisData
        {
            public double InstAttention { get; set; }
            public double InstRelaxation { get; set; }
            public double RelAttention { get; set; }
            public double RelRelaxation { get; set; }
            public double Alpha { get; set; }
            public double Beta { get; set; }
            public double Gamma { get; set; }
            public double Theta { get; set; }
            public double Delta { get; set; }
            public DateTime Timestamp { get; set; }
            public TimeSpan VideoTime { get; set; }
        }
        
        public async Task<bool> StartAnalysis(TimeSpan videoDuration)
        {
            if (_isAnalyzing)
                return false;
            
            try
            {
                _isAnalyzing = true;
                _cts = new CancellationTokenSource();
                _analysisStartTime = DateTime.Now;
                
                AnalysisStateChanged?.Invoke(true);
                
                _analysisTask = Task.Run(async () =>
                {
                    await InitializeDeviceAsync();
                    await StartAnalysisLoopAsync(videoDuration);
                }, _cts.Token);
                
                return true;
            }
            catch (Exception ex)
            {
                StopAnalysis();
                throw new Exception($"Ошибка запуска анализа: {ex.Message}", ex);
            }
        }
        
        private async Task InitializeDeviceAsync()
        {
            // =================== Поиск устройств ===================
            _scanner = new Scanner(SensorFamily.SensorLEBrainBit);
            var foundEvent = new TaskCompletionSource<bool>();
            
            _scanner.EventSensorsChanged += (scanner, sensors) =>
            {
                if (sensors.Count > 0)
                    foundEvent.TrySetResult(true);
            };
            
            _scanner.Start();
            await Task.WhenAny(foundEvent.Task, Task.Delay(15000));
            
            if (!foundEvent.Task.IsCompleted)
            {
                _scanner.Stop();
                throw new Exception("Устройство BrainBit не найдено за 15 секунд");
            }
            
            // =================== Подключение к устройству ===================
            var sensorInfo = _scanner.Sensors[0];
            _sensor = _scanner.CreateSensor(sensorInfo) as BrainBitSensor;
            
            if (_sensor == null)
                throw new Exception("Не удалось подключиться к BrainBit");
            
            // =================== Настройка математики ===================
            var mls = new MathLibSetting
            {
                sampling_rate = SamplingFrequency,
                process_win_freq = 25,
                n_first_sec_skipped = 4,
                fft_window = SamplingFrequency * 4,
                bipolar_mode = true,
                channels_number = 20,
                channel_for_analysis = 10
            };
            
            var ads = new ArtifactDetectSetting
            {
                art_bord = 110,
                allowed_percent_artpoints = 70,
                raw_betap_limit = 800_000,
                total_pow_border = (uint)(40 * 1e7),
                global_artwin_sec = 4,
                spect_art_by_totalp = true,
                num_wins_for_quality_avg = 125,
                hanning_win_spectrum = false,
                hamming_win_spectrum = true
            };
            
            var mss = new MentalAndSpectralSetting
            {
                n_sec_for_averaging = 2,
                n_sec_for_instant_estimation = 4
            };
            
            _math = new EegEmotionalMath(mls, ads, mss);
            _math.SetCallibrationLength(20);
            _math.StartCalibration();
            
            // =================== Запуск калибровки ===================
            await Task.Delay(20000); // 20 секунд на калибровку
            _isCalibrated = true;
            
            // =================== Запуск сбора данных ===================
            _sensor.EventBrainBitSignalDataRecived += OnBrainBitSignalDataRecived;
            _sensor.ExecCommand(SensorCommand.CommandStartSignal);
        }
        
        private void OnBrainBitSignalDataRecived(ISensor sensor, BrainBitSignalData[] data)
        {
            if (_math == null || !_isCalibrated || _cts?.IsCancellationRequested == true)
                return;
            
            try
            {
                RawChannels[] bipolars = new RawChannels[data.Length];
                for (var i = 0; i < data.Length; i++)
                {
                    bipolars[i] = new RawChannels
                    {
                        LeftBipolar = data[i].T3 - data[i].O1,
                        RightBipolar = data[i].T4 - data[i].O2
                    };
                }
                
                _math.PushBipolars(bipolars);
                _math.ProcessDataArr();
                
                var mentalData = _math.ReadMentalDataArr();
                var spData = _math.ReadSpectralDataPercentsArr();
                
                if (mentalData.Length > 0 && spData.Length > 0)
                {
                    var lastMental = mentalData[mentalData.Length - 1];
                    var lastSpec = spData[spData.Length - 1];
                    
                    var analysisData = new AnalysisData
                    {
                        InstAttention = lastMental.InstAttention,
                        InstRelaxation = lastMental.InstRelaxation,
                        RelAttention = lastMental.RelAttention,
                        RelRelaxation = lastMental.RelRelaxation,
                        Alpha = lastSpec.Alpha,
                        Beta = lastSpec.Beta,
                        Gamma = lastSpec.Gamma,
                        Theta = lastSpec.Theta,
                        Delta = lastSpec.Delta,
                        Timestamp = DateTime.Now,
                        VideoTime = _analysisStartTime.HasValue 
                            ? DateTime.Now - _analysisStartTime.Value 
                            : TimeSpan.Zero
                    };
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AnalysisDataReceived?.Invoke(analysisData);
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки данных BrainBit: {ex.Message}");
            }
        }
        
        private async Task StartAnalysisLoopAsync(TimeSpan videoDuration)
        {
            var startTime = DateTime.Now;
            
            while (!_cts.Token.IsCancellationRequested)
            {
                var elapsed = DateTime.Now - startTime;
                if (elapsed >= videoDuration)
                {
                    StopAnalysis();
                    break;
                }
                
                await Task.Delay(1000, _cts.Token);
            }
        }
        
        public void StopAnalysis()
        {
            _isAnalyzing = false;
            
            _cts?.Cancel();
            _analysisTask?.Wait(5000);
            
            if (_sensor != null)
            {
                _sensor.EventBrainBitSignalDataRecived -= OnBrainBitSignalDataRecived;
                _sensor.ExecCommand(SensorCommand.CommandStopSignal);
                _sensor.Disconnect();
                _sensor.Dispose();
                _sensor = null;
            }
            
            _scanner?.Stop();
            _scanner?.Dispose();
            _scanner = null;
            
            _math?.Dispose();
            _math = null;
            
            AnalysisStateChanged?.Invoke(false);
        }
        
        public void Dispose()
        {
            StopAnalysis();
            _cts?.Dispose();
        }
    }
}