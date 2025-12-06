using System.Windows;

namespace EmotionAnalyzer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Добавляем настройки для четкости отображения
            ConfigureRenderingForSharpness();
            
            base.OnStartup(e);
        }

        private void ConfigureRenderingForSharpness()
        {
            // Способ 1: Отключить аппаратное ускорение (для четких границ)
            // System.Windows.Interop.RenderOptions.ProcessRenderMode = 
            //     System.Windows.Interop.RenderMode.SoftwareOnly;
            
            // Способ 2: Включить ClearType для текста
            // Эта настройка доступна только в разметке XAML
            
            // Способ 3: Настройки через DependencyProperty
            // Используем альтернативный подход
        }
    }
}