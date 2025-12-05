using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;

namespace ScenaryBuilder
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            dialog.Title = "Выберите папку, куда будет сохраняться сценарий";

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                ProjectPathTextBox.Text = dialog.FolderName;
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            string name = ProjectNameTextBox.Text.Trim();
            string basePath = ProjectPathTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(basePath))
            {
                MessageBox.Show("Введите имя проекта и путь.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string projectFolder = Path.Combine(basePath, name);
            
            try
            {
                // Создаем папку проекта, если ее нет
                if (!Directory.Exists(projectFolder))
                {
                    Directory.CreateDirectory(projectFolder);
                }

                string filePath = Path.Combine(projectFolder, "scenary.json");

                // Создаем базовую структуру проекта с одной строкой данных
                var projectStructure = new Dictionary<string, object>
                {
                    [name] = new Dictionary<string, object>
                    {
                        ["Number"] = new List<string> { "1" },
                        ["Action"] = new List<string> { "Новое действие" },
                        ["Description"] = new List<string> { "Описание кадра" }
                    }
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string jsonString = JsonSerializer.Serialize(projectStructure, options);
                File.WriteAllText(filePath, jsonString);

                var editor = new Editor(projectFolder, filePath, name);
                editor.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании проекта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            dialog.Title = "Выберите папку проекта";

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                string folderPath = dialog.FolderName;
                string jsonFile = Path.Combine(folderPath, "scenary.json");

                if (!File.Exists(jsonFile))
                {
                    MessageBox.Show("Файл scenary.json не найден в выбранной папке.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    string projectName = new DirectoryInfo(folderPath).Name;
                    var editor = new Editor(folderPath, jsonFile, projectName);
                    editor.Show();
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии проекта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}