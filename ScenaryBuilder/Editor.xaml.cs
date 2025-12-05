using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System;
using Path = System.IO.Path;

namespace ScenaryBuilder
{
    public partial class Editor : Window
    {
        private DataTable table = new DataTable();
        private bool isLoaded = false;

        private readonly string projectFolder;
        private readonly string jsonPath;
        private readonly string projectName;
        private readonly string imagesFolder;
        private readonly string soundsFolder;

        // Словарь для хранения информации о типах колонок
        private Dictionary<string, string> columnTypes = new Dictionary<string, string>();

        // Переменные для управления аудио
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer progressTimer;
        private bool isDraggingProgress = false;
        private string currentAudioFile = "";
        private string currentAudioColumnName = "";
        private int currentAudioRowIndex = -1;
        private bool isPlaying = false;

        
        private DataGridColumnHeader selectedColumnHeader;

        // Класс для хранения информации о типе столбца
        public class ColumnType
        {
            public string Name { get; set; }
            public ImageSource Image { get; set; }
            public string Type { get; set; }
        }

        public Editor(string projectFolder, string jsonPath, string projectName)
        {
            InitializeComponent();

            this.projectFolder = projectFolder;
            this.jsonPath = jsonPath;
            this.projectName = projectName;
            this.imagesFolder = Path.Combine(projectFolder, "images");
            this.soundsFolder = Path.Combine(projectFolder, "sounds");

            // Создаем папки для изображений и звуков
            Directory.CreateDirectory(imagesFolder);
            Directory.CreateDirectory(soundsFolder);

            // Инициализация аудиоплеера
            InitializeAudioPlayer();

            InitializeColumnTypes();
            InitializeTable();
            LoadProject();
            isLoaded = true;

            ProjectFolderTextView.Text = projectFolder;

            // Инициализация счетчика слов
            UpdateWordsCount();
        }

        private void InitializeAudioPlayer()
        {
            // Настройка MediaPlayer
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

            // Настройка таймера для обновления прогресса
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromMilliseconds(100);
            progressTimer.Tick += ProgressTimer_Tick;
        }

        private void MediaPlayer_MediaFailed(object sender, ExceptionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Ошибка воспроизведения аудио: {e.ErrorException.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StopAudio();
            });
        }

        private void MediaPlayer_MediaOpened(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateTimeDisplay(0);
                // Сбрасываем слайдер при открытии нового файла
                ProgressSlider.Value = 0;
            });
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StopAudio();
            });
        }

        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            if (!isDraggingProgress && mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                double progress = mediaPlayer.Position.TotalMilliseconds / 
                                 mediaPlayer.NaturalDuration.TimeSpan.TotalMilliseconds;
                // Обновляем слайдер только если не перетаскиваем
                if (!isDraggingProgress)
                {
                    ProgressSlider.Value = progress;
                }
                UpdateTimeDisplay(progress);
            }
        }

        private void ActionsTable_CurrentCellChanged(object sender, EventArgs e)
        {
            if (ActionsTable.CurrentCell != null)
            {
                DataGridColumn column = ActionsTable.CurrentCell.Column;
                int rowIndex = ActionsTable.Items.IndexOf(ActionsTable.CurrentCell.Item);

                if (column != null && rowIndex >= 0)
                {
                    string columnName = column.Header.ToString();
                    
                    // Проверяем, является ли столбец аудио-столбцом
                    if (columnTypes.ContainsKey(columnName) && columnTypes[columnName] == "audio")
                    {
                        // Получаем имя файла из ячейки
                        DataRowView rowView = ActionsTable.CurrentCell.Item as DataRowView;
                        if (rowView != null)
                        {
                            currentAudioFile = rowView[columnName]?.ToString() ?? "";
                            currentAudioColumnName = columnName;
                            currentAudioRowIndex = rowIndex;
                            
                            // Загружаем данные в плейер
                            LoadAudioToPlayer();
                        }
                    }
                    else
                    {
                        // Скрываем панель плейера, если выбрана не аудио-ячейка
                        PlayerPanel.Visibility = Visibility.Collapsed;
                        StopAudio();
                    }
                }
            }
            
            // Обновляем счетчик слов при изменении выбранной ячейки
            UpdateWordsCount();
        }

        private void LoadAudioToPlayer()
        {
            if (!string.IsNullOrEmpty(currentAudioFile))
            {
                try
                {
                    string fullPath = Path.Combine(soundsFolder, currentAudioFile);
                    if (File.Exists(fullPath))
                    {
                        // Показываем панель плейера
                        PlayerPanel.Visibility = Visibility.Visible;
                        AudioFileNameText.Text = currentAudioFile;
                        
                        // Останавливаем текущее воспроизведение
                        StopAudio();
                        
                        // Открываем файл, но не воспроизводим
                        mediaPlayer.Open(new Uri(fullPath));
                        
                        // Сбрасываем слайдер
                        ProgressSlider.Value = 0;
                        UpdateTimeDisplay(0);
                    }
                    else
                    {
                        // Файл не найден
                        PlayerPanel.Visibility = Visibility.Visible;
                        AudioFileNameText.Text = "Файл не найден";
                        StopAudio();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки аудио: {ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    PlayerPanel.Visibility = Visibility.Visible;
                    AudioFileNameText.Text = "Ошибка загрузки";
                    StopAudio();
                }
            }
            else
            {
                // Пустая ячейка
                PlayerPanel.Visibility = Visibility.Visible;
                AudioFileNameText.Text = "Нет звука";
                StopAudio();
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            PlayAudio();
        }

        private void PlayAudio()
        {
            if (!string.IsNullOrEmpty(currentAudioFile))
            {
                try
                {
                    string fullPath = Path.Combine(soundsFolder, currentAudioFile);
                    if (File.Exists(fullPath))
                    {
                        if (mediaPlayer.Source == null)
                        {
                            mediaPlayer.Open(new Uri(fullPath));
                        }
                        
                        mediaPlayer.Play();
                        isPlaying = true;
                        progressTimer.Start();
                        
                        // Меняем видимость кнопок
                        PlayButton.Visibility = Visibility.Collapsed;
                        PauseButton.Visibility = Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка воспроизведения: {ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
            {
                mediaPlayer.Pause();
                isPlaying = false;
                progressTimer.Stop();
                
                // Меняем видимость кнопок
                PauseButton.Visibility = Visibility.Collapsed;
                PlayButton.Visibility = Visibility.Visible;
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopAudio();
        }

        private void StopAudio()
        {
            mediaPlayer.Stop();
            isPlaying = false;
            progressTimer.Stop();
            ProgressSlider.Value = 0;
            
            // Меняем видимость кнопок
            PauseButton.Visibility = Visibility.Collapsed;
            PlayButton.Visibility = Visibility.Visible;
        }

        // Методы для управления Slider
        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDraggingProgress = true;
            // Паузим воспроизведение при начале перетаскивания
            if (isPlaying)
            {
                mediaPlayer.Pause();
            }
            progressTimer.Stop();
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDraggingProgress && mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                // Устанавливаем позицию воспроизведения
                double progress = ProgressSlider.Value;
                var duration = mediaPlayer.NaturalDuration.TimeSpan;
                var newPosition = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * progress);
                mediaPlayer.Position = newPosition;
                
                // Сбрасываем флаг перетаскивания
                isDraggingProgress = false;
                
                // Обновляем отображение времени
                UpdateTimeDisplay(progress);
                
                // Продолжаем воспроизведение если было запущено
                if (isPlaying)
                {
                    mediaPlayer.Play();
                    progressTimer.Start();
                }
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isDraggingProgress)
            {
                return;
            }
            
            // Обновляем отображение времени при перетаскивании
            UpdateTimeDisplay(ProgressSlider.Value);
        }

        private void UpdateTimeDisplay(double progress)
        {
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var duration = mediaPlayer.NaturalDuration.TimeSpan;
                TimeSpan currentTime;
                
                if (isDraggingProgress)
                {
                    currentTime = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * progress);
                }
                else
                {
                    currentTime = mediaPlayer.Position;
                }
                
                // Форматируем время
                string currentTimeStr = $"{(int)currentTime.TotalMinutes}:{currentTime.Seconds:00}";
                string durationStr = $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
                
                TimeText.Text = $"{currentTimeStr} / {durationStr}";
            }
            else
            {
                TimeText.Text = "0:00 / 0:00";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // Останавливаем аудио и освобождаем ресурсы
            StopAudio();
            mediaPlayer.Close();
            progressTimer.Stop();
        }

        private void InitializeTable()
        {
            table = new DataTable();
            
            // Создаем стандартные колонки
            table.Columns.Add("Number", typeof(int));
            table.Columns.Add("Действие", typeof(string));

            // Инициализируем словарь типов для стандартных колонок
            columnTypes["Number"] = "text";
            columnTypes["Действие"] = "text";

            // Подписываемся на события изменения таблицы
            table.TableNewRow += (s, e) => 
            { 
                if (isLoaded) 
                {
                    SaveProject();
                    UpdateWordsCount();
                }
            };
            
            table.RowChanged += (s, e) => 
            { 
                if (isLoaded) 
                {
                    SaveProject();
                    UpdateWordsCount();
                }
            };
            
            table.RowDeleted += (s, e) => 
            { 
                if (isLoaded) 
                {
                    SaveProject();
                    UpdateWordsCount();
                }
            };
            
            table.ColumnChanged += (s, e) => 
            { 
                if (isLoaded) 
                {
                    SaveProject();
                    UpdateWordsCount();
                }
            };

            ActionsTable.ItemsSource = table.DefaultView;
            
            // Подписываемся на событие CurrentCellChanged
            ActionsTable.CurrentCellChanged += ActionsTable_CurrentCellChanged;
            
            // Подписка на события для отслеживания выделения ячеек и снятия фокуса
            ActionsTable.SelectedCellsChanged += ActionsTable_SelectedCellsChanged;
            ActionsTable.PreviewKeyDown += ActionsTable_PreviewKeyDown;
            ActionsTable.LostFocus += ActionsTable_LostFocus;
            
            SetupColumns();
        }

        private void InitializeColumnTypes()
        {
            var columnTypesList = new List<ColumnType>
            {
                new ColumnType 
                { 
                    Name = "Текст", 
                    Image = new BitmapImage(new Uri("pack://application:,,,/Resources/text2.ico", UriKind.Absolute)),
                    Type = "text"
                },
                new ColumnType 
                { 
                    Name = "Звук", 
                    Image = new BitmapImage(new Uri("pack://application:,,,/Resources/sound.ico")),
                    Type = "audio"
                },
                new ColumnType 
                { 
                    Name = "Картинка", 
                    Image = new BitmapImage(new Uri("pack://application:,,,/Resources/picture.ico")),
                    Type = "image"
                }
            };
            
            ColumnTypeComboBox.ItemsSource = columnTypesList;
        }

        private void SaveProject()
        {
            try
            {
                var root = new Dictionary<string, object>();
                var inner = new Dictionary<string, object>();

                // Сохраняем информацию о типах колонок
                inner["_columnTypes"] = columnTypes;

                // Собираем данные из всех колонок
                foreach (DataColumn col in table.Columns)
                {
                    var values = new List<string>();
                    foreach (DataRow row in table.Rows)
                    {
                        if (row.RowState == DataRowState.Deleted) continue;
                        values.Add(row[col.ColumnName]?.ToString() ?? "");
                    }
                    inner[col.ColumnName] = values;
                }

                root[projectName] = inner;
                
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                string json = JsonSerializer.Serialize(root, options);
                File.WriteAllText(jsonPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProject()
        {
            if (!File.Exists(jsonPath))
                return;

            try
            {
                string json = File.ReadAllText(jsonPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json);

                if (dict != null && dict.ContainsKey(projectName))
                {
                    var inner = dict[projectName];

                    // Загружаем информацию о типах колонок
                    if (inner.ContainsKey("_columnTypes"))
                    {
                        var columnTypesJson = inner["_columnTypes"].ToString();
                        columnTypes = JsonSerializer.Deserialize<Dictionary<string, string>>(columnTypesJson);
                    }

                    // Очищаем существующие колонки (кроме стандартных)
                    var columnsToRemove = table.Columns.Cast<DataColumn>()
                        .Where(c => c.ColumnName != "Number" && c.ColumnName != "Действие")
                        .ToList();

                    foreach (var col in columnsToRemove)
                    {
                        table.Columns.Remove(col);
                    }

                    // Добавляем дополнительные колонки из JSON
                    foreach (var columnName in inner.Keys)
                    {
                        if (columnName != "_columnTypes" && !table.Columns.Contains(columnName))
                        {
                            table.Columns.Add(columnName, typeof(string));
                        }
                    }

                    // Определяем количество строк
                    int rowCount = 0;
                    foreach (var item in inner)
                    {
                        if (item.Key != "_columnTypes" && item.Value is JsonElement jsonElement)
                        {
                            if (jsonElement.ValueKind == JsonValueKind.Array)
                            {
                                rowCount = jsonElement.GetArrayLength();
                                break;
                            }
                        }
                    }

                    // Очищаем строки
                    table.Rows.Clear();

                    // Заполняем данные
                    for (int i = 0; i < rowCount; i++)
                    {
                        var row = table.NewRow();
                        
                        foreach (DataColumn col in table.Columns)
                        {
                            if (inner.ContainsKey(col.ColumnName) && inner[col.ColumnName] is JsonElement jsonElement)
                            {
                                if (jsonElement.ValueKind == JsonValueKind.Array)
                                {
                                    var values = JsonSerializer.Deserialize<List<string>>(jsonElement.GetRawText());
                                    if (i < values.Count)
                                    {
                                        // Для колонки Number преобразуем в int
                                        if (col.ColumnName == "Number" && int.TryParse(values[i], out int number))
                                        {
                                            row[col] = number;
                                        }
                                        else
                                        {
                                            row[col] = values[i];
                                        }
                                    }
                                }
                            }
                        }
                        
                        table.Rows.Add(row);
                    }

                    // Обновляем отображение колонок в DataGrid
                    SetupColumns();
                    
                    // Обновляем счетчик слов
                    UpdateWordsCount();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке проекта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupColumns()
        {
            ActionsTable.Columns.Clear();

            // Колонка Number
            var numberColumn = new DataGridTextColumn
            {
                Header = "№",
                Binding = new Binding("Number"),
                Width = 50,
                IsReadOnly = true
            };
            numberColumn.ElementStyle = CreateTextBlockStyle(ActionsTable.FontSize);
            ActionsTable.Columns.Add(numberColumn);

            // Создаем колонки для всех остальных полей
            foreach (DataColumn dataColumn in table.Columns)
            {
                if (dataColumn.ColumnName == "Number") continue;

                DataGridColumn column;
                string columnType = columnTypes.ContainsKey(dataColumn.ColumnName) ? columnTypes[dataColumn.ColumnName] : "text";

                switch (columnType)
                {
                    case "audio":
                        column = CreateAudioColumn(dataColumn.ColumnName);
                        break;
                    case "image":
                        column = CreateImageColumn(dataColumn.ColumnName);
                        break;
                    default: // text
                        column = new DataGridTextColumn
                        {
                            Header = dataColumn.ColumnName,
                            Binding = new Binding(dataColumn.ColumnName),
                            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                        };
                        ApplyTextColumnStyle(column as DataGridTextColumn, ActionsTable.FontSize);
                        break;
                }

                ActionsTable.Columns.Add(column);
            }
        }

        private DataGridTemplateColumn CreateAudioColumn(string columnName)
        {
            var column = new DataGridTemplateColumn
            {
                Header = columnName,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };

            // Шаблон для отображения
            var cellTemplate = new DataTemplate();
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(8));

            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            stackFactory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            // Иконка аудио
            var iconFactory = new FrameworkElementFactory(typeof(Image));
            iconFactory.SetBinding(Image.SourceProperty, new Binding(columnName) 
            { 
                Converter = new AudioIconConverter(),
                ConverterParameter = soundsFolder
            });
            iconFactory.SetValue(Image.WidthProperty, 24.0);
            iconFactory.SetValue(Image.HeightProperty, 24.0);
            iconFactory.SetValue(Image.MarginProperty, new Thickness(0, 0, 8, 0));

            // Текст с именем файла
            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(TextBlock.TextProperty, new Binding(columnName));
            textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            textFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            textFactory.SetValue(TextBlock.FontSizeProperty, ActionsTable.FontSize);

            stackFactory.AppendChild(iconFactory);
            stackFactory.AppendChild(textFactory);
            borderFactory.AppendChild(stackFactory);
            cellTemplate.VisualTree = borderFactory;
            column.CellTemplate = cellTemplate;

            // Шаблон для редактирования
            var editingTemplate = new DataTemplate();
            var editBorderFactory = new FrameworkElementFactory(typeof(Border));
            editBorderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            editBorderFactory.SetValue(Border.PaddingProperty, new Thickness(8));

            var textBoxFactory = new FrameworkElementFactory(typeof(TextBox));
            textBoxFactory.SetBinding(TextBox.TextProperty, new Binding(columnName));
            textBoxFactory.SetValue(TextBox.BackgroundProperty, Brushes.Transparent);
            textBoxFactory.SetValue(TextBox.ForegroundProperty, Brushes.White);
            textBoxFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
            textBoxFactory.SetValue(TextBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBoxFactory.SetValue(TextBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textBoxFactory.SetValue(TextBox.PaddingProperty, new Thickness(8, 2, 8, 2));
            textBoxFactory.SetValue(TextBox.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            textBoxFactory.SetValue(TextBox.FontSizeProperty, ActionsTable.FontSize);
            textBoxFactory.SetValue(TextBox.IsReadOnlyProperty, true);
            textBoxFactory.SetValue(TextBox.TextAlignmentProperty, TextAlignment.Center);

            // Обработчики событий для аудио
            textBoxFactory.AddHandler(TextBox.MouseDoubleClickEvent, new MouseButtonEventHandler(AudioCell_MouseDoubleClick));
            textBoxFactory.AddHandler(TextBox.DropEvent, new DragEventHandler(AudioCell_Drop));
            textBoxFactory.AddHandler(TextBox.PreviewDragOverEvent, new DragEventHandler(AudioCell_PreviewDragOver));

            editBorderFactory.AppendChild(textBoxFactory);
            editingTemplate.VisualTree = editBorderFactory;
            column.CellEditingTemplate = editingTemplate;

            return column;
        }

        private DataGridTemplateColumn CreateImageColumn(string columnName)
        {
            var column = new DataGridTemplateColumn
            {
                Header = columnName,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };

            // Шаблон для отображения
            var cellTemplate = new DataTemplate();
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(8));

            var containerFactory = new FrameworkElementFactory(typeof(Border));
            containerFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            containerFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            containerFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
            containerFactory.SetValue(Border.MaxWidthProperty, 120.0);
            containerFactory.SetValue(Border.MaxHeightProperty, 80.0);

            // Контейнер для изображения
            var imageFactory = new FrameworkElementFactory(typeof(Image));
            imageFactory.SetBinding(Image.SourceProperty, new Binding(columnName) 
            { 
                Converter = new ImagePreviewConverter(),
                ConverterParameter = imagesFolder
            });
            imageFactory.SetValue(Image.StretchProperty, Stretch.Uniform);
            imageFactory.SetValue(Image.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            imageFactory.SetValue(Image.VerticalAlignmentProperty, VerticalAlignment.Center);
            imageFactory.SetValue(Image.MaxWidthProperty, 100.0);
            imageFactory.SetValue(Image.MaxHeightProperty, 60.0);
            imageFactory.SetValue(Image.MarginProperty, new Thickness(4));

            containerFactory.AppendChild(imageFactory);
            borderFactory.AppendChild(containerFactory);
            cellTemplate.VisualTree = borderFactory;
            column.CellTemplate = cellTemplate;

            // Шаблон для редактирования
            var editingTemplate = new DataTemplate();
            var editBorderFactory = new FrameworkElementFactory(typeof(Border));
            editBorderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            editBorderFactory.SetValue(Border.PaddingProperty, new Thickness(8));

            var textBoxFactory = new FrameworkElementFactory(typeof(TextBox));
            textBoxFactory.SetBinding(TextBox.TextProperty, new Binding(columnName));
            textBoxFactory.SetValue(TextBox.BackgroundProperty, Brushes.Transparent);
            textBoxFactory.SetValue(TextBox.ForegroundProperty, Brushes.White);
            textBoxFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
            textBoxFactory.SetValue(TextBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBoxFactory.SetValue(TextBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textBoxFactory.SetValue(TextBox.PaddingProperty, new Thickness(8, 2, 8, 2));
            textBoxFactory.SetValue(TextBox.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            textBoxFactory.SetValue(TextBox.FontSizeProperty, ActionsTable.FontSize);
            textBoxFactory.SetValue(TextBox.IsReadOnlyProperty, true);
            textBoxFactory.SetValue(TextBox.TextAlignmentProperty, TextAlignment.Center);

            // Обработчики событий для изображений
            textBoxFactory.AddHandler(TextBox.MouseDoubleClickEvent, new MouseButtonEventHandler(ImageCell_MouseDoubleClick));
            textBoxFactory.AddHandler(TextBox.DropEvent, new DragEventHandler(ImageCell_Drop));
            textBoxFactory.AddHandler(TextBox.PreviewDragOverEvent, new DragEventHandler(ImageCell_PreviewDragOver));

            editBorderFactory.AppendChild(textBoxFactory);
            editingTemplate.VisualTree = editBorderFactory;
            column.CellEditingTemplate = editingTemplate;

            return column;
        }

        // Конвертер для иконок аудио
        public class AudioIconConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string fileName = value as string;
                string soundsFolder = parameter as string;

                if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(soundsFolder))
                {
                    string filePath = Path.Combine(soundsFolder, fileName);
                    if (File.Exists(filePath))
                    {
                        return new BitmapImage(new Uri("pack://application:,,,/Resources/sound.ico"));
                    }
                }

                return new BitmapImage(new Uri("pack://application:,,,/Resources/sound.ico"));
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        // Конвертер для предпросмотра изображений
        public class ImagePreviewConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string fileName = value as string;
                string imagesFolder = parameter as string;

                if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(imagesFolder))
                {
                    string filePath = Path.Combine(imagesFolder, fileName);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(filePath);
                            bitmap.DecodePixelWidth = 100;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            return bitmap;
                        }
                        catch
                        {
                            // В случае ошибки возвращаем иконку по умолчанию
                        }
                    }
                }

                return new BitmapImage(new Uri("pack://application:,,,/Resources/picture.ico"));
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        private Style CreateTextBlockStyle(double fontSize)
        {
            return new Style(typeof(TextBlock))
            {
                Setters =
                {
                    new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center),
                    new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center),
                    new Setter(TextBlock.ForegroundProperty, Brushes.White),
                    new Setter(TextBlock.FontSizeProperty, fontSize)
                }
            };
        }

        private void ApplyTextColumnStyle(DataGridTextColumn column, double fontSize)
        {
            column.ElementStyle = new Style(typeof(TextBlock))
            {
                Setters =
                {
                    new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap),
                    new Setter(TextBlock.PaddingProperty, new Thickness(5, 2, 5, 2)),
                    new Setter(TextBlock.ForegroundProperty, Brushes.White),
                    new Setter(TextBlock.FontSizeProperty, fontSize)
                }
            };
            column.EditingElementStyle = new Style(typeof(TextBox))
            {
                Setters =
                {
                    new Setter(TextBox.BackgroundProperty, Brushes.Transparent),
                    new Setter(TextBox.ForegroundProperty, Brushes.White),
                    new Setter(TextBox.PaddingProperty, new Thickness(5, 2, 5, 2)),
                    new Setter(TextBox.BorderThicknessProperty, new Thickness(0)),
                    new Setter(TextBox.AcceptsReturnProperty, true),
                    new Setter(TextBox.TextWrappingProperty, TextWrapping.Wrap),
                    new Setter(TextBox.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto),
                    new Setter(TextBox.FontSizeProperty, fontSize)
                }
            };
        }

        private void AddColumnButton_Click(object sender, RoutedEventArgs e)
        {
            string columnName = GenerateColumnName();
            
            var selectedType = ColumnTypeComboBox.SelectedItem as ColumnType;
            string columnType = selectedType?.Type ?? "text";

            columnTypes[columnName] = columnType;

            table.Columns.Add(columnName, typeof(string));

            DataGridColumn newColumn;
            
            switch (columnType)
            {
                case "audio":
                    newColumn = CreateAudioColumn(columnName);
                    break;
                case "image":
                    newColumn = CreateImageColumn(columnName);
                    break;
                default: // text
                    newColumn = new DataGridTextColumn
                    {
                        Header = columnName,
                        Binding = new Binding(columnName),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    };
                    ApplyTextColumnStyle(newColumn as DataGridTextColumn, ActionsTable.FontSize);
                    break;
            }

            ActionsTable.Columns.Add(newColumn);
            SaveProject();
            UpdateWordsCount();
        }

        private string GenerateColumnName()
        {
            var existingColumnNames = table.Columns.Cast<DataColumn>()
                .Select(col => col.ColumnName)
                .Where(name => name.StartsWith("Столбец"))
                .ToList();

            if (existingColumnNames.Count == 0)
                return "Столбец 1";

            var existingNumbers = new List<int>();
            foreach (var name in existingColumnNames)
            {
                string numberPart = name.Substring("Столбец".Length).Trim();
                if (int.TryParse(numberPart, out int number))
                {
                    existingNumbers.Add(number);
                }
            }

            if (existingNumbers.Count == 0)
                return "Столбец 1";

            existingNumbers.Sort();

            for (int i = 1; i <= existingNumbers.Count + 1; i++)
            {
                if (!existingNumbers.Contains(i))
                {
                    return $"Столбец {i}";
                }
            }

            return $"Столбец {existingNumbers.Count + 1}";
        }

        private void ImageCell_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico|All files (*.*)|*.*";
                openFileDialog.Title = "Выберите изображение";
                
                if (openFileDialog.ShowDialog() == true)
                {
                    ProcessImageFile(openFileDialog.FileName, textBox);
                }
            }
        }

        private void ImageCell_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string extension = Path.GetExtension(files[0]).ToLower();
                    if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp" || extension == ".ico")
                    {
                        e.Effects = DragDropEffects.Copy;
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
            }
            e.Handled = true;
        }

        private void ImageCell_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && sender is TextBox textBox)
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string extension = Path.GetExtension(files[0]).ToLower();
                    if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp" || extension == ".ico")
                    {
                        ProcessImageFile(files[0], textBox);
                    }
                }
            }
        }

        private void ProcessImageFile(string filePath, TextBox textBox)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(imagesFolder, fileName);
                
                File.Copy(filePath, destPath, true);
                textBox.Text = fileName;
                
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
                SaveProject();
                UpdateWordsCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AudioCell_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Audio Files (*.mp3;*.wav;*.ogg)|*.mp3;*.wav;*.ogg|All files (*.*)|*.*";
                openFileDialog.Title = "Выберите аудио файл";
                
                if (openFileDialog.ShowDialog() == true)
                {
                    ProcessAudioFile(openFileDialog.FileName, textBox);
                }
            }
        }

        private void AudioCell_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string extension = Path.GetExtension(files[0]).ToLower();
                    if (extension == ".mp3" || extension == ".wav" || extension == ".ogg")
                    {
                        e.Effects = DragDropEffects.Copy;
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
            }
            e.Handled = true;
        }

        private void AudioCell_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && sender is TextBox textBox)
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string extension = Path.GetExtension(files[0]).ToLower();
                    if (extension == ".mp3" || extension == ".wav" || extension == ".ogg")
                    {
                        ProcessAudioFile(files[0], textBox);
                    }
                }
            }
        }

        private void ProcessAudioFile(string filePath, TextBox textBox)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(soundsFolder, fileName);
                
                File.Copy(filePath, destPath, true);
                textBox.Text = fileName;
                
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
                SaveProject();
                UpdateWordsCount();
                
                if (textBox.DataContext is DataRowView rowView && 
                    ActionsTable.CurrentCell != null && 
                    ActionsTable.CurrentCell.Item == rowView)
                {
                    currentAudioFile = fileName;
                    LoadAudioToPlayer();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке аудио файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveProject();
            this.Close();
        }

        private void FontSizeBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextNumeric(e.Text);
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = (string)e.DataObject.GetData(DataFormats.Text);
                if (!IsTextNumeric(text))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        private bool IsTextNumeric(string text)
        {
            foreach (char c in text)
                if (!char.IsDigit(c))
                    return false;
            return true;
        }

        private void FontSizeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyFontSize();
            }
        }

        private void ApplyFontSize()
        {
            if (int.TryParse(FontSizeBox.Text, out int fontSize) && fontSize > 0 && fontSize <= 72)
            {
                ActionsTable.FontSize = fontSize;
                SetupColumns();
            }
            else
            {
                MessageBox.Show("Введите корректный размер шрифта (1-72)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                FontSizeBox.Text = "12";
                ActionsTable.FontSize = 12;
                SetupColumns();
            }
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        // =================== Методы для подсчета слов ===================

        // Метод для подсчета слов в тексте
        private int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;
            
            // Разделяем текст на слова, игнорируя пробелы и пустые строки
            return text.Split(new[] { ' ', '\t', '\n', '\r' }, 
                             StringSplitOptions.RemoveEmptyEntries).Length;
        }

        // Метод для подсчета слов во всей таблице
        private int CountWordsInTable()
        {
            int totalWords = 0;
            
            foreach (DataRow row in table.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;
                
                foreach (DataColumn column in table.Columns)
                {
                    if (column.ColumnName == "Number") continue; // Пропускаем колонку с номерами
                    
                    var value = row[column]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        totalWords += CountWords(value);
                    }
                }
            }
            
            return totalWords;
        }

        // Метод для подсчета слов в выбранной ячейке
        private int CountWordsInSelectedCell()
        {
            if (ActionsTable.CurrentCell != null && 
                ActionsTable.CurrentCell.Item is DataRowView rowView)
            {
                var column = ActionsTable.CurrentCell.Column;
                if (column != null)
                {
                    string columnName = column.Header.ToString();
                    var value = rowView[columnName]?.ToString();
                    return CountWords(value ?? "");
                }
            }
            
            return 0;
        }

        // Метод для обновления отображения счетчика слов
        private void UpdateWordsCount()
        {
            int wordCount = 0;
            
            if (ActionsTable.CurrentCell != null && ActionsTable.CurrentCell.IsValid)
            {
                // Если есть выбранная ячейка - считаем слова в ней
                wordCount = CountWordsInSelectedCell();
                WordsCountText.Text = $"Слов в ячейке: {wordCount}";
            }
            else
            {
                // Если нет выбранной ячейки - считаем слова во всей таблице
                wordCount = CountWordsInTable();
                WordsCountText.Text = $"Слов в таблице: {wordCount}";
            }
        }

        // Обработчик изменения выбранных ячеек
        private void ActionsTable_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            UpdateWordsCount();
        }

        // Обработчик нажатия клавиш (для снятия фокуса по Ctrl)
        private void ActionsTable_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl + любая клавиша для снятия фокуса
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // Снимаем фокус с DataGrid
                ActionsTable.UnselectAllCells();
                ActionsTable.SelectedItem = null;
                
                // Переводим фокус на другой элемент (например, на окно)
                FocusManager.SetFocusedElement(this, this);
                
                // Обновляем счетчик слов
                
                e.Handled = true;
                UpdateWordsCount();

                return;
            }
            
            // Обновляем счетчик при нажатии Enter (после редактирования)
            if (e.Key == Key.Enter)
            {
                // Небольшая задержка для обновления данных
                Dispatcher.BeginInvoke(new Action(() => UpdateWordsCount()), DispatcherPriority.Background);
            }
        }

        // Обработчик потери фокуса DataGrid
        private void ActionsTable_LostFocus(object sender, RoutedEventArgs e)
        {
            // Обновляем счетчик при потере фокуса
            UpdateWordsCount();
        }

        // Обработчик изменения данных в таблице
        private void Table_DataChanged(object sender, EventArgs e)
        {
            if (isLoaded)
            {
                UpdateWordsCount();
            }
        }
    }
}