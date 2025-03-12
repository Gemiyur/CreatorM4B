using Microsoft.Win32;
using System.Collections.Generic;
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

namespace CreatorM4B
{
    /// <summary>
    /// Класс главного окна.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string CreateButtonNewText = "Новый";

        private readonly string CreateButtonCreateText;

        private readonly BackgroundWorker Worker = new()
        {
            //WorkerSupportsCancellation = true,
            WorkerReportsProgress = true
        };

        public MainWindow()
        {
            InitializeComponent();
            CreateButtonCreateText = (string)CreateButton.Content;
            Worker.DoWork += Worker_DoWork;
            Worker.ProgressChanged += Worker_ProgressChanged;
            Worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        private void CheckCreateButton()
        {
            CreateButton.IsEnabled = FolderTextBox.Text.Any() && FileTextBox.Text.Any();
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
            CreateButton.Content = CreateButtonNewText;
            CreateButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog()
            {
                AddToRecent = false,
                Title = "Папка с файлами книги",
            };
            if (folderDialog.ShowDialog() != true)
                return;
            FolderTextBox.Text = folderDialog.FolderName;
            var folderInfo = new DirectoryInfo(FolderTextBox.Text);
            var files = folderInfo.GetFiles();
            var sb = new StringBuilder();
            foreach (var file in files)
                sb.Append(file == files.First() ? file : "\n" + file);
            FilesTextBox.Text = sb.ToString();
            CheckCreateButton();
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
            var filename = Path.GetExtension(fileDialog.FileName).Equals(".m4b", StringComparison.CurrentCultureIgnoreCase)
                ? fileDialog.FileName
                : fileDialog.FileName + ".m4b";
            FileTextBox.Text = filename;
            CheckCreateButton();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if ((string)CreateButton.Content == CreateButtonNewText)
            {
                CreateButton.Content = CreateButtonCreateText;
                FolderTextBox.Text = string.Empty;
                FileTextBox.Text = string.Empty;
                StatusTextBlock.Text = string.Empty;
                FilesTextBox.Text = string.Empty;
                FolderButton.IsEnabled = true;
                FileButton.IsEnabled = true;
                CheckCreateButton();
                return;
            }
            FolderButton.IsEnabled = false;
            FileButton.IsEnabled = false;
            CreateButton.IsEnabled = false;
            CloseButton.IsEnabled = false;
            StatusTextBlock.Text = "Создание...";
            object[] parameters = [FolderTextBox.Text, FileTextBox.Text, AllImagesCheckBox.IsChecked == true];
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