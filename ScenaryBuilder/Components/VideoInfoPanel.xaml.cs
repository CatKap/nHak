using System.Windows;
using System.Windows.Controls;

namespace EmotionAnalyzer.Components
{
    public partial class VideoInfoPanel : UserControl
    {
        public VideoInfoPanel()
        {
            InitializeComponent();
        }

        // Метод для загрузки видео в плеер
        public void LoadVideo(string videoPath)
        {
            // Загрузка видео в контейнер VideoPlayerContainer
            // Будет реализовано другими разработчиками
        }

        // Метод для обновления информации о видео
        public void UpdateVideoInfo(string title, string format, string size, string quality, string duration)
        {
            // Обновление полей с информацией о видео
            // Будет реализовано другими разработчиками
        }

        // Обработчик

    }
}