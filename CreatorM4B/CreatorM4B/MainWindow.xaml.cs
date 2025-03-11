using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CreatorM4B
{
    /// <summary>
    /// Класс главного окна.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly BackgroundWorker Worker = new()
        {
            WorkerSupportsCancellation = true,
            WorkerReportsProgress = true
        };

        public MainWindow()
        {
            InitializeComponent();
            Worker.DoWork += Worker_DoWork;
            Worker.ProgressChanged += Worker_ProgressChanged;
            Worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        private FileInfo? Merge(string folderName, string fileName, bool allImages, BackgroundWorker? worker, DoWorkEventArgs e)
        {
            return null;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Worker.IsBusy == true)
            {
                MessageBox.Show("Выполняется операция.", Title);
                e.Cancel = true;
            }
        }

        private void Worker_DoWork(object? sender, DoWorkEventArgs e)
        {
            var parameters = (object[]?)e.Argument;
            BackgroundWorker? worker = sender as BackgroundWorker;
            e.Result = parameters != null
                ? Merge((string)parameters[0], (string)parameters[1], (bool)parameters[2], worker, e)
                : null;
        }

        private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            StatusTextBlock.Text = (string)e.UserState;
        }

        private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show(e.Error.Message);
                StatusTextBlock.Text = "Ошибка";
            }
            else
            {
                StatusTextBlock.Text = "Готово";
            }
            var result = (FileInfo?)e.Result;
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void FileButton_Click(object sender, RoutedEventArgs e)
        {
            var fileDialog = new SaveFileDialog()
            {
                AddToRecent = false,
                DefaultExt = ".m4b",
                Title = "Создать файл книги",
                Filter = $"Файлы книг|*.m4b"
            };
            if (fileDialog.ShowDialog() != true)
                return;
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (Worker.IsBusy == true)
            {
                MessageBox.Show("Выполняется предыдущая операция.", Title);
                return;
            }
            object[] parameters = [FolderTextBox.Text, FileTextBox.Text, AllImagesCheckBox.IsChecked == true];
            StatusTextBlock.Text = "Создание...";
            Worker.RunWorkerAsync(parameters);
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            new AboutDialog() { Owner = this }.ShowDialog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}