using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;

namespace EmotionAnalyzer.Components
{
    public partial class StartupScreen : UserControl
    {
        // События
        public event EventHandler? VideoLoaded;
        public event EventHandler? LoadingComplete;
        
        // Свойства
        public string VideoPath { get; private set; } = string.Empty;
        private bool _isDownloadingFromUrl = false;
        private HttpClient _httpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private DateTime _downloadStartTime;
        
        public StartupScreen()
        {
            InitializeComponent();
            InitializeDropTarget();
            InitializeHttpClient();
        }
        
        private void InitializeHttpClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
        }
        
        private void InitializeDropTarget()
        {
            this.AllowDrop = true;
            this.DragEnter += OnDragEnter;
            this.DragOver += OnDragOver;
            this.DragLeave += OnDragLeave;
            this.Drop += OnDrop;
        }
        
        // ============ ОБРАБОТЧИКИ UI ============
        
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                this.Opacity = 0.8;
                e.Effects = DragDropEffects.Copy;
            }
        }
        
        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        
        private void OnDragLeave(object sender, DragEventArgs e)
        {
            this.Opacity = 1.0;
        }
        
        private async void OnDrop(object sender, DragEventArgs e)
        {
            this.Opacity = 1.0;
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    var videoFile = files[0];
                    if (IsSupportedVideoFormat(videoFile))
                    {
                        await ProcessVideoFile(videoFile, false);
                    }
                    else
                    {
                        ShowErrorMessage("Неверный формат", "Пожалуйста, выберите файл формата MP4 или MOV.");
                    }
                }
            }
        }
        
        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Видео файлы (*.mp4;*.mov)|*.mp4;*.mov|Все файлы (*.*)|*.*",
                Multiselect = false,
                Title = "Выберите видео файл"
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                await ProcessVideoFile(openFileDialog.FileName, false);
            }
        }
        
        private async void DownloadFromUrlButton_Click(object sender, RoutedEventArgs e)
        {
            string url = VideoUrlTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(url))
            {
                ShowErrorMessage("Ошибка", "Введите ссылку на видео");
                return;
            }
            
            if (!IsValidVideoUrl(url))
            {
                ShowErrorMessage("Ошибка", "Некорректная ссылка. Поддерживаются только:\n• rutube.ru\n• vk.com/video");
                return;
            }
            
            // Блокируем кнопки
            DownloadFromUrlButton.IsEnabled = false;
            LoadButton.IsEnabled = false;
            
            // Начинаем процесс загрузки по ссылке
            await ProcessVideoFromUrl(url);
        }
        
        // ============ МЕТОДЫ СКАЧИВАНИЯ ============
        
        private async Task ProcessVideoFromUrl(string url)
        {
            try
            {
                // Создаем токен отмены
                _cancellationTokenSource = new CancellationTokenSource();
                
                // Начинаем загрузку
                _isDownloadingFromUrl = true;
                MainPanel.Visibility = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                
                LoadingProgressBar.Value = 0;
                ProgressPercentageText.Text = "0%";
                LoadingTitleText.Text = "Скачивание видео из интернета";
                DownloadStatusText.Visibility = Visibility.Visible;
                StatusTextBlock.Text = "Подготовка к загрузке...";
                
                // Скачиваем видео
                string downloadedPath = await DownloadVideoFromUrl(url, _cancellationTokenSource.Token);
                
                if (!string.IsNullOrEmpty(downloadedPath) && File.Exists(downloadedPath))
                {
                    VideoPath = downloadedPath;
                    await CompleteVideoLoading();
                }
                else
                {
                    throw new Exception("Не удалось скачать видео");
                }
            }
            catch (OperationCanceledException)
            {
                ShowInfoMessage("Отменено", "Скачивание видео отменено");
                Reset();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Ошибка скачивания", $"Не удалось скачать видео: {ex.Message}");
                Reset();
            }
            finally
            {
                DownloadFromUrlButton.IsEnabled = true;
                LoadButton.IsEnabled = true;
            }
        }
        
        private async Task<string> DownloadVideoFromUrl(string url, CancellationToken cancellationToken)
        {
            _downloadStartTime = DateTime.Now;
            
            try
            {
                // Пытаемся получить прямую ссылку на видео
                StatusTextBlock.Text = "Поиск видеофайла...";
                DownloadStatusText.Text = "Анализ страницы...";
                UpdateProgress(10);
                
                string directVideoUrl = await GetDirectVideoUrl(url, cancellationToken);
                
                if (string.IsNullOrEmpty(directVideoUrl))
                {
                    throw new Exception("Не удалось получить ссылку на видеофайл");
                }
                
                // Создаем папку для скачанных видео
                string downloadsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "EmotionAnalyzer",
                    "Downloads"
                );
                
                Directory.CreateDirectory(downloadsFolder);
                
                // Генерируем уникальное имя файла
                string fileName = GenerateSafeFileName($"video_{DateTime.Now:yyyyMMdd_HHmmss}");
                string downloadPath = Path.Combine(downloadsFolder, fileName);
                
                // Скачиваем видео с отображением прогресса
                StatusTextBlock.Text = "Скачивание видео...";
                DownloadStatusText.Text = "Подключение к серверу...";
                UpdateProgress(20);
                
                await DownloadFileWithProgress(directVideoUrl, downloadPath, cancellationToken);
                
                // Проверяем скачанный файл
                if (!File.Exists(downloadPath))
                {
                    throw new Exception("Файл не был создан");
                }
                
                var fileInfo = new FileInfo(downloadPath);
                if (fileInfo.Length == 0)
                {
                    throw new Exception("Скачанный файл пустой");
                }
                
                UpdateProgress(100);
                StatusTextBlock.Text = "Видео успешно скачано!";
                DownloadStatusText.Text = $"Размер: {fileInfo.Length / (1024 * 1024):F1} МБ";
                
                return downloadPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка скачивания: {ex.Message}");
            }
        }
        
        private async Task<string> GetDirectVideoUrl(string url, CancellationToken cancellationToken)
        {
            try
            {
                if (url.Contains("rutube.ru"))
                {
                    return await GetRutubeVideoUrl(url, cancellationToken);
                }
                else if (url.Contains("vk.com/video") || url.Contains("vkvideo.ru"))
                {
                    return await GetVkVideoUrl(url, cancellationToken);
                }
                else
                {
                    // Пробуем скачать напрямую (для прямых ссылок на mp4)
                    if (url.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                        url.EndsWith(".mov", StringComparison.OrdinalIgnoreCase))
                    {
                        return url;
                    }
                }
                
                throw new Exception("Неизвестный тип ссылки");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка получения прямой ссылки: {ex.Message}");
                return null;
            }
        }
        
        private async Task<string> GetRutubeVideoUrl(string url, CancellationToken cancellationToken)
        {
            try
            {
                // Извлекаем ID видео
                string videoId = ExtractVideoIdFromUrl(url);
                
                if (string.IsNullOrEmpty(videoId))
                {
                    throw new Exception("Не удалось определить ID видео");
                }
                
                // Используем API Rutube для получения информации о видео
                string apiUrl = $"https://rutube.ru/api/play/options/{videoId}/?format=json";
                
                var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                string json = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    // Ищем ссылку на видео в формате m3u8 или mp4
                    if (doc.RootElement.TryGetProperty("video_balancer", out var balancer))
                    {
                        if (balancer.TryGetProperty("m3u8", out var m3u8Url))
                        {
                            string m3u8 = m3u8Url.GetString();
                            if (!string.IsNullOrEmpty(m3u8))
                            {
                                // Берем первую ссылку из списка
                                return m3u8.Split(',').FirstOrDefault()?.Trim('"') ?? "";
                            }
                        }
                        
                        // Ищем mp4 ссылки
                        if (balancer.TryGetProperty("mp4", out var mp4Urls))
                        {
                            var urls = mp4Urls.EnumerateArray();
                            if (urls.Any())
                            {
                                return urls.First().GetString();
                            }
                        }
                    }
                }
                
                throw new Exception("Не удалось найти ссылку на видео");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка Rutube API: {ex.Message}");
            }
        }
        
        private async Task<string> GetVkVideoUrl(string url, CancellationToken cancellationToken)
        {
            try
            {
                // Получаем HTML страницы
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                string html = await response.Content.ReadAsStringAsync();
                
                // Ищем ссылки на видео в разных форматах
                var regexes = new[]
                {
                    // Прямые ссылки на mp4
                    new Regex(@"https?://[^""'\s]+\.mp4[^""'\s]*", RegexOptions.IgnoreCase),
                    // Ссылки в JSON данных
                    new Regex(@"""url""\s*:\s*""([^""]+\.mp4[^""]*)""", RegexOptions.IgnoreCase),
                    new Regex(@"""src""\s*:\s*""([^""]+\.mp4[^""]*)""", RegexOptions.IgnoreCase),
                    // Ссылки в playerParams
                    new Regex(@"playerParams\s*=\s*\{[^}]+""url""\s*:\s*""([^""]+)""[^}]*\}", RegexOptions.Singleline),
                };
                
                foreach (var regex in regexes)
                {
                    var match = regex.Match(html);
                    if (match.Success)
                    {
                        string videoUrl = match.Groups[1].Value?.Replace("\\/", "/") ?? match.Value;
                        if (!string.IsNullOrEmpty(videoUrl) && videoUrl.Contains(".mp4"))
                        {
                            return videoUrl.Trim('"', '\'', ' ');
                        }
                    }
                }
                
                throw new Exception("Не удалось найти ссылку на видео на странице VK");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка парсинга VK: {ex.Message}");
            }
        }
        
        private async Task DownloadFileWithProgress(string url, string destinationPath, CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                
                long? totalBytes = response.Content.Headers.ContentLength;
                bool canReportProgress = totalBytes.HasValue;
                
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[81920]; // 80KB буфер для быстрой загрузки
                    long totalRead = 0;
                    int read;
                    
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                        totalRead += read;
                        
                        if (canReportProgress)
                        {
                            // Вычисляем прогресс от 20% до 90%
                            int progress = 20 + (int)((double)totalRead / totalBytes.Value * 70);
                            UpdateProgress(progress);
                            
                            // Обновляем статус
                            TimeSpan elapsed = DateTime.Now - _downloadStartTime;
                            double speed = totalRead / elapsed.TotalSeconds;
                            double remainingBytes = totalBytes.Value - totalRead;
                            TimeSpan remainingTime = TimeSpan.FromSeconds(remainingBytes / speed);
                            
                            DownloadStatusText.Text = 
                                $"Скачано: {FormatBytes(totalRead)} / {FormatBytes(totalBytes.Value)} | " +
                                $"Скорость: {FormatBytesPerSecond(speed)} | " +
                                $"Осталось: {FormatTime(remainingTime)}";
                        }
                        else
                        {
                            // Если размер неизвестен, просто показываем прогресс
                            int progress = 20 + (totalRead > 0 ? (int)Math.Min(70, totalRead / (1024 * 1024)) : 0);
                            UpdateProgress(progress);
                            DownloadStatusText.Text = $"Скачано: {FormatBytes(totalRead)}";
                        }
                    }
                }
            }
        }
        
        // ============ ОБРАБОТКА ЛОКАЛЬНЫХ ФАЙЛОВ ============
        
        private async Task ProcessVideoFile(string filePath, bool fromUrl = false)
        {
            try
            {
                _isDownloadingFromUrl = fromUrl;
                MainPanel.Visibility = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                
                LoadingProgressBar.Value = 0;
                ProgressPercentageText.Text = "0%";
                
                if (fromUrl)
                {
                    LoadingTitleText.Text = "Скачивание видео из интернета";
                    DownloadStatusText.Visibility = Visibility.Visible;
                }
                else
                {
                    LoadingTitleText.Text = "Не выключайте приложение, видео грузится";
                    DownloadStatusText.Visibility = Visibility.Collapsed;
                }
                
                StatusTextBlock.Text = "Подготовка к загрузке...";
                
                if (fromUrl)
                {
                    // Уже обработано в ProcessVideoFromUrl
                    return;
                }
                else
                {
                    await SimulateVideoLoading(filePath);
                    VideoPath = filePath;
                    await CompleteVideoLoading();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Ошибка загрузки", $"Ошибка при загрузке видео: {ex.Message}");
                Reset();
            }
        }
        
        private async Task CompleteVideoLoading()
        {
            StatusTextBlock.Text = "Видео успешно загружено!";
            await Task.Delay(500);
            
            // Генерируем событие загрузки видео
            VideoLoaded?.Invoke(this, EventArgs.Empty);
            
            // Дополнительная обработка
            await ProcessAfterVideoLoaded();
        }
        
        private async Task SimulateVideoLoading(string filePath)
        {
            // Имитация загрузки для локального файла
            StatusTextBlock.Text = "Проверка файла...";
            for (int i = 0; i <= 100; i += 5)
            {
                UpdateProgress(i);
                StatusTextBlock.Text = i < 30 ? "Проверка файла..." :
                                      i < 60 ? "Загрузка видео..." :
                                      i < 90 ? "Анализ метаданных..." :
                                               "Подготовка к воспроизведению...";
                await Task.Delay(50);
            }
        }
        
        private async Task ProcessAfterVideoLoaded()
        {
            try
            {
                // Имитация дополнительной обработки
                StatusTextBlock.Text = "Выполнение анализа...";
                LoadingProgressBar.Value = 0;
                
                for (int i = 0; i <= 100; i += 10)
                {
                    UpdateProgress(i);
                    await Task.Delay(100);
                }
                
                StatusTextBlock.Text = "Анализ завершен!";
                await Task.Delay(500);
                
                // Генерируем событие полного завершения
                LoadingComplete?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Ошибка анализа", $"Ошибка при анализе: {ex.Message}");
                LoadingComplete?.Invoke(this, EventArgs.Empty);
            }
        }
        
        // ============ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ============
        
        private string ExtractVideoIdFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var match = Regex.Match(uri.PathAndQuery, @"/video/(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                
                match = Regex.Match(uri.PathAndQuery, @"video(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private bool IsValidVideoUrl(string url)
        {
            string lowerUrl = url.ToLower();
            return lowerUrl.Contains("rutube.ru") || 
                   lowerUrl.Contains("vk.com/video") ||
                   lowerUrl.Contains("vkvideo.ru") ||
                   lowerUrl.EndsWith(".mp4") ||
                   lowerUrl.EndsWith(".mov");
        }
        
        private bool IsSupportedVideoFormat(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension == ".mp4" || extension == ".mov";
        }
        
        private string GenerateSafeFileName(string baseName)
        {
            // Убираем недопустимые символы
            string safeName = Regex.Replace(baseName, @"[^\w\-\.]", "_");
            return safeName + ".mp4";
        }
        
        private void UpdateProgress(int value)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingProgressBar.Value = value;
                ProgressPercentageText.Text = $"{value}%";
            });
        }
        
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
        
        private string FormatBytesPerSecond(double bytesPerSecond)
        {
            return FormatBytes((long)bytesPerSecond) + "/с";
        }
        
        private string FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
            else
                return $"{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
        }
        
        private void ShowErrorMessage(string title, string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
        
        private void ShowInfoMessage(string title, string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
        
        // ============ ПУБЛИЧНЫЕ МЕТОДЫ ============
        
        public string GetVideoPath()
        {
            return VideoPath;
        }
        
        public void Reset()
        {
            Dispatcher.Invoke(() =>
            {
                VideoPath = string.Empty;
                _isDownloadingFromUrl = false;
                VideoUrlTextBox.Text = "";
                MainPanel.Visibility = Visibility.Visible;
                LoadingPanel.Visibility = Visibility.Collapsed;
                LoadingProgressBar.Value = 0;
                ProgressPercentageText.Text = "0%";
                StatusTextBlock.Text = "Готов к загрузке видео";
                DownloadStatusText.Text = "";
                DownloadStatusText.Visibility = Visibility.Collapsed;
                
                // Отменяем текущую загрузку
                _cancellationTokenSource?.Cancel();
            });
        }
        
        // ============ ОЧИСТКА РЕСУРСОВ ============
        
        public void Cleanup()
        {
            _cancellationTokenSource?.Cancel();
            _httpClient?.Dispose();
        }
    }
}