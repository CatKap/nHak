using System.Windows;
using System.Windows.Controls;

namespace EmotionAnalyzer.Components
{
    public partial class FeedBackPanel : UserControl
    {
        public FeedBackPanel()
        {
            InitializeComponent();
        }

        // События для подписки из главного окна
        public event RoutedEventHandler OpenFullResultsClicked;
        public event RoutedEventHandler OpenFeedbackClicked;

        private void OpenFullResultsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFullResultsClicked?.Invoke(sender, e);
        }

        private void OpenFeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFeedbackClicked?.Invoke(sender, e);
        }

        // Свойства для настройки извне
        public string FullResultsButtonText
        {
            get => OpenFullResultsButton.Content.ToString();
            set => OpenFullResultsButton.Content = value;
        }

        public string FeedbackButtonText
        {
            get => OpenFeedbackButton.Content.ToString();
            set => OpenFeedbackButton.Content = value;
        }

        // Свойства для ширины кнопок
        public double ButtonWidth
        {
            get => OpenFullResultsButton.Width;
            set
            {
                OpenFullResultsButton.Width = value;
                OpenFeedbackButton.Width = value;
            }
        }

        // Свойства для отступов
        public Thickness ButtonMargin
        {
            get => OpenFullResultsButton.Margin;
            set
            {
                OpenFullResultsButton.Margin = value;
                OpenFeedbackButton.Margin = value;
            }
        }
    }
}