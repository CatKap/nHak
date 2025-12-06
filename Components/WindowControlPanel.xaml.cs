using System.Windows;
using System.Windows.Controls;

namespace EmotionAnalyzer.Components.WindowControls
{
    public partial class WindowControlPanel : UserControl
    {
        // События для кнопок
        public event RoutedEventHandler MinimizeClick
        {
            add { MinimizeBtn.Click += value; }
            remove { MinimizeBtn.Click -= value; }
        }
        
        public event RoutedEventHandler CloseClick
        {
            add { CloseBtn.Click += value; }
            remove { CloseBtn.Click -= value; }
        }

        public WindowControlPanel()
        {
            InitializeComponent();
        }
    }
}