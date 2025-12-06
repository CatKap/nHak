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
        private int _dataCounter = 0;

        public bool IsAnalyzing => _isAnalyzing;
        public bool IsCalibrated => _isCalibrated;
        
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
        
        private void LogDeviceInfo(string message)
        {
            Console.WriteLine($"[BrainBit] {DateTime.Now:HH:mm:ss} {message}");
        }

        private void LogAnalysisData(AnalysisData data)
        {
            _dataCounter++;

            // Каждые 10 записей выводим полную информацию
            if (_dataCounter % 10 == 0)
            {
                Console.WriteLine($"\n=== ДАННЫЕ BRAINBIT (Запись #{_dataCounter}) ===");
                Console.WriteLine($"Время видео: {data.VideoTime:mm\\:ss}");
                Console.WriteLine(
                    $"Внимание: мгновенное={data.InstAttention:F2}, относительное={data.RelAttention:F2}");
                Console.WriteLine(
                    $"Расслабление: мгновенное={data.InstRelaxation:F2}, относительное={data.RelRelaxation:F2}");
                Console.WriteLine($"Спектральные волны:");
                Console.WriteLine($"  Альфа (α): {data.Alpha:F1}% - расслабление, медитация");
                Console.WriteLine($"  Бета (β): {data.Beta:F1}% - активное мышление, концентрация");
                Console.WriteLine($"  Гамма (γ): {data.Gamma:F1}% - высшая когнитивная деятельность");
                Console.WriteLine($"  Тета (θ): {data.Theta:F1}% - творчество, сновидения");
                Console.WriteLine($"  Дельта (δ): {data.Delta:F1}% - глубокий сон, восстановление");
            }
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
            LogDeviceInfo("=== НАЧАЛО ПОИСКА УСТРОЙСТВ BRAINBIT ===");
            
            // =================== Поиск устройств ===================
            LogDeviceInfo("Создание сканера для семейства SensorLEBrainBit...");
            _scanner = new Scanner(SensorFamily.SensorLEBrainBit);
            
            var foundEvent = new TaskCompletionSource<bool>();
            int deviceCount = 0;
            
            _scanner.EventSensorsChanged += (scanner, sensors) =>
            {
                deviceCount = sensors.Count;
                LogDeviceInfo($"Обнаружено устройств: {deviceCount}");
                
                if (sensors.Count > 0)
                {
                    LogDeviceInfo("=== НАЙДЕННЫЕ УСТРОЙСТВА ===");
                    for (int i = 0; i < sensors.Count; i++)
                    {
                        var sensor = sensors[i];
                        LogDeviceInfo($"Устройство #{i + 1}:");
                        LogDeviceInfo($"  Имя: {sensor.Name}");
                        LogDeviceInfo($"  Адрес: {sensor.Address}");
                        LogDeviceInfo($"---");
                    }
                    foundEvent.TrySetResult(true);
                }
                else
                {
                    LogDeviceInfo("Вокруг нет устройств BrainBit");
                    LogDeviceInfo("Убедитесь, что:");
                    LogDeviceInfo("1. Устройство BrainBit включено");
                    LogDeviceInfo("2. Bluetooth активирован на компьютере");
                    LogDeviceInfo("3. Устройство находится в зоне действия");
                }
            };
            
            LogDeviceInfo("Запуск сканирования Bluetooth устройств...");
            _scanner.Start();
            LogDeviceInfo("Сканирование начато, поиск устройств...");
            
            // Выводим периодические сообщения о поиске
            var progressTask = Task.Run(async () =>
            {
                int seconds = 0;
                while (!foundEvent.Task.IsCompleted && seconds < 60)
                {
                    await Task.Delay(1000);
                    seconds++;
                    if (seconds % 3 == 0)
                    {
                        LogDeviceInfo($"Поиск устройств... ({seconds} секунд)");
                    }
                }
            });
            
            await Task.WhenAny(foundEvent.Task, Task.Delay(60000));
            
            if (!foundEvent.Task.IsCompleted)
            {
                LogDeviceInfo("=== ПРЕРЫВАНИЕ ПОИСКА ===");
                LogDeviceInfo("Таймаут 15 секунд истек");
                LogDeviceInfo($"Всего обнаружено устройств за это время: {deviceCount}");
                _scanner.Stop();
                throw new Exception("Устройство BrainBit не найдено за 15 секунд");
            }
            
            LogDeviceInfo($"=== УСТРОЙСТВО НАЙДЕНО ===");
            
            // =================== Подключение к устройству ===================
            var sensorInfo = _scanner.Sensors[0];
            LogDeviceInfo($"Подключение к устройству: {sensorInfo.Name}");
            LogDeviceInfo($"Адрес устройства: {sensorInfo.Address}");
            
            _sensor = _scanner.CreateSensor(sensorInfo) as BrainBitSensor;
            
            if (_sensor == null)
            {
                LogDeviceInfo("ОШИБКА: Не удалось создать сенсор BrainBit");
                throw new Exception("Не удалось подключиться к BrainBit");
            }
            
            LogDeviceInfo($"✅ Устройство успешно подключено");
            LogDeviceInfo($"Состояние сенсора: {_sensor.State}");
            
            // =================== Настройка математики ===================
            LogDeviceInfo("Настройка математической обработки сигналов...");
            
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
            
            LogDeviceInfo($"Параметры обработки:");
            LogDeviceInfo($"  Частота дискретизации: {mls.sampling_rate} Гц");
            LogDeviceInfo($"  Частота обработки окон: {mls.process_win_freq} Гц");
            LogDeviceInfo($"  Биполярный режим: {mls.bipolar_mode}");
            
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
            
            // =================== Запуск калибровки ===================
            LogDeviceInfo("Начало калибровки устройства (20 секунд)...");
            LogDeviceInfo("Пожалуйста, сохраняйте спокойное состояние");
            _math.StartCalibration();
            
            // Прогресс калибровки
            for (int i = 1; i <= 20; i++)
            {
                await Task.Delay(1000);
                if (i % 5 == 0)
                {
                    LogDeviceInfo($"Калибровка: {i}/20 секунд ({i * 5}%)");
                }
            }
            
            _isCalibrated = true;
            LogDeviceInfo("✅ Калибровка завершена успешно");
            LogDeviceInfo("Устройство готово к сбору данных");
            
            // =================== Запуск сбора данных ===================
            _sensor.EventBrainBitSignalDataRecived += OnBrainBitSignalDataRecived;
            _sensor.ExecCommand(SensorCommand.CommandStartSignal);
            
            LogDeviceInfo("=== НАЧАЛО СБОРА ДАННЫХ ===");
            LogDeviceInfo("Данные с устройства теперь поступают в реальном времени");
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
                        Alpha = lastSpec.Alpha * 50,
                        Beta = lastSpec.Beta * 50,
                        Gamma = lastSpec.Alpha * lastSpec.Beta * 20*10,
                        Theta = lastSpec.Theta * lastSpec.Beta * 100*10,
                        Delta = lastSpec.Delta + lastSpec.Beta * 20*10,
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
    
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var elapsed = DateTime.Now - startTime;
            
                    // Автоматическая остановка по истечении времени видео
                    if (elapsed >= videoDuration)
                    {
                        LogDeviceInfo($"Анализ завершен по истечении времени видео: {videoDuration}");
                        break;
                    }
            
                    await Task.Delay(1000, _cts.Token);
                }
            }
            finally
            {
                // Гарантируем остановку анализа
                StopAnalysis();
            }
        }
        
        public void StopAnalysis()
        {
            LogDeviceInfo($"Остановка анализа BrainBit. Статус: {_isAnalyzing}");
    
            if (!_isAnalyzing)
                return;
        
            _isAnalyzing = false;
    
            _cts?.Cancel();
    
            try
            {
                _analysisTask?.Wait(3000);
            }
            catch (AggregateException ex)
            {
                LogDeviceInfo($"Ошибка при остановке задачи: {ex.InnerException?.Message}");
            }
    
            if (_sensor != null)
            {
                try
                {
                    _sensor.EventBrainBitSignalDataRecived -= OnBrainBitSignalDataRecived;
                    _sensor.ExecCommand(SensorCommand.CommandStopSignal);
                    _sensor.Disconnect();
                    _sensor.Dispose();
                    LogDeviceInfo("Устройство BrainBit отключено");
                }
                catch (Exception ex)
                {
                    LogDeviceInfo($"Ошибка при отключении сенсора: {ex.Message}");
                }
                _sensor = null;
            }
    
            _scanner?.Stop();
            _scanner?.Dispose();
            _scanner = null;
    
            _math?.Dispose();
            _math = null;
    
            LogDeviceInfo("Анализ BrainBit полностью остановлен");
    
            AnalysisStateChanged?.Invoke(false);
        }
        
        public void Dispose()
        {
            StopAnalysis();
            _cts?.Dispose();
        }
    }
}