using System.Windows;
using System.Windows.Controls;

namespace EmotionAnalyzer.Components
{
    public partial class FeedbackForm : UserControl
    {
        public event RoutedEventHandler FormSubmitted;
        public event RoutedEventHandler FormCancelled;

        public FeedbackForm()
        {
            InitializeComponent();
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            FormSubmitted?.Invoke(this, e);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            FormCancelled?.Invoke(this, e);
        }

        private void StarButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Реализуют другие разработчики
        }
    }
}