using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using NeuroSDK;

namespace WpfApp2;

public partial class Instruction : Window
{
    private Scanner scanner;
    private BrainBitSensor sensor;
    private DispatcherTimer updateTimer;
    
    private double o1Value = 0;
    private double o2Value = 0;
    private double t3Value = 0;
    private double t4Value = 0;
    
    public Instruction()
    {
        InitializeComponent();
        InitializeSensor();
        InitializeUpdateTimer();
    }
    
    private void InitializeSensor()
    {
        Console.WriteLine("Searching for BrainBit device...");
        scanner = new Scanner(SensorFamily.SensorLEBrainBit);
        scanner.EventSensorsChanged += OnDeviceFound;
        scanner.Start();
    }
    
    private void InitializeUpdateTimer()
    {
        updateTimer = new DispatcherTimer();
        updateTimer.Interval = TimeSpan.FromMilliseconds(100);
        updateTimer.Tick += UpdateStatusIndicators;
        updateTimer.Start();
    }
    
    private void UpdateStatusIndicators(object sender, EventArgs e)
    {
        // Обновляем индикаторы на основе текущих значений
        UpdateIndicator(O1StatusIndicator, o1Value, "O1");
        UpdateIndicator(O2StatusIndicator, o2Value, "O2");
        UpdateIndicator(T3StatusIndicator, t3Value, "T3");
        UpdateIndicator(T4StatusIndicator, t4Value, "T4");
    }
    
    private void UpdateIndicator(Ellipse indicator, double value, string channelName)
    {
        
        if (value > 2000)
        {
            indicator.Fill = new SolidColorBrush(Colors.DodgerBlue);
        }
        else
        {
            indicator.Fill = new SolidColorBrush(Colors.DimGray);
        }
        
        if (updateTimer.IsEnabled)
        {
            Random rand = new Random();
            value = rand.Next(800, 1200); // Случайные значения от 800 до 1200
        }
    }
    
    public void Checker()
    {

        o1Value = 1100;
        o2Value = 950;
        t3Value = 1200;
        t4Value = 1050;
    }
    
    private void OnDeviceFound(IScanner scanner, IReadOnlyList<SensorInfo> sensors)
    {
        Dispatcher.Invoke(() =>
        {
            Console.WriteLine($"Found {sensors.Count} devices");
            foreach (var sensorInfo in sensors)
            {
                Console.WriteLine($"Connecting to {sensorInfo.Name} ({sensorInfo.Address})");
                
                sensor = scanner.CreateSensor(sensorInfo) as BrainBitSensor;
                if (sensor != null)
                {
                    Console.WriteLine($"Successfully connected to {sensorInfo.Name}!");
                    
                    sensor.EventBrainBitSignalDataRecived += Sensor_EventBrainBitSignalDataRecived;
                    sensor.EventBrainBitResistDataRecived += Sensor_EventBrainBitResistDataRecived;
                    
                    sensor.ExecCommand(SensorCommand.CommandStartSignal);

                }
            }
        });
    }
    
    private void Sensor_EventBrainBitSignalDataRecived(ISensor sensor, BrainBitSignalData[] data)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (BrainBitSignalData signal in data)
            {
                o1Value = signal.O1 * 1e6;
                o2Value = signal.O2 * 1e6;
                t3Value = signal.T3 * 1e6;
                t4Value = signal.T4 * 1e6;
                
            }
        });
    }
    
    private void Sensor_EventBrainBitResistDataRecived(ISensor sensor, BrainBitResistData data)
    {
        Dispatcher.Invoke(() =>
        {
            // o1Value = data.O1 / 1e3;
            // o2Value = data.O2 / 1e3;
            // t3Value = data.T3 / 1e3;
            // t4Value = data.T4 / 1e3;
        });
    }
    
    private void StatisticsButton_Click(object sender, RoutedEventArgs e)
    {
        if (updateTimer != null)
        {
            updateTimer.Stop();
            updateTimer.Tick -= UpdateStatusIndicators;
        }
        
        if (sensor != null)
        {
            sensor.ExecCommand(SensorCommand.CommandStopSignal);
            sensor.Disconnect();
            sensor.Dispose();
            sensor = null;
        }
        
        if (scanner != null)
        {
            scanner.Stop();
            scanner.EventSensorsChanged -= OnDeviceFound;
            scanner.Dispose();
            scanner = null;
        }
        
        this.Close();
    }
    
    protected override void OnClosed(EventArgs e)
    {
        if (updateTimer != null)
        {
            updateTimer.Stop();
        }
        
        if (sensor != null)
        {
            sensor.Disconnect();
            sensor.Dispose();
        }
        
        if (scanner != null)
        {
            scanner.Stop();
            scanner.Dispose();
        }
        
        base.OnClosed(e);
    }
}