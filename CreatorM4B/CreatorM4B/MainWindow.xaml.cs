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
using ATL;
using NAudio.Wave;
using System.Linq;

namespace CreatorM4B
{
    /// <summary>
    /// Класс главного окна.
    /// </summary>
    public partial class MainWindow : Window
    {
        private class FileItem(string filename)
        {
            public string Name = filename;

            public Track Track = new(filename);
        }

        private const string CreateButtonNewText = "Новый";

        private readonly string CreateButtonCreateText;

        private readonly BackgroundWorker Worker = new()
        {
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

        private FileInfo? Merge(string folderName, string fileName, bool allImages, BackgroundWorker? worker)
        {
            worker?.ReportProgress(0, "сбор данных...");
            var folder = new DirectoryInfo(folderName);
            var fileItems = folder.GetFiles("*.mp3")
                .Select(x => new FileItem(x.FullName))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            if (fileItems == null || !fileItems.Any())
            {
                return null;
            }

            var mergedMP3 = Path.GetTempFileName(); // Имя объединённого временного файла mp3.
            var mergedM4A = mergedMP3 + ".m4a";     // Имя объединённого временного файла m4a.

            try
            {
                // Создаём объединённый временный файл mp3, состоящий из всех файлов mp3 в папке.
                worker?.ReportProgress(0, "объединение файлов...");
                using (var stream = new FileStream(mergedMP3, FileMode.OpenOrCreate))
                {
                    foreach (var file in fileItems)
                    {
                        var reader = new Mp3FileReader(file.Name);
                        if ((stream.Position == 0) && (reader.Id3v2Tag != null))
                        {
                            stream.Write(reader.Id3v2Tag.RawData, 0, reader.Id3v2Tag.RawData.Length);
                        }
                        Mp3Frame frame;
                        while ((frame = reader.ReadNextFrame()) != null)
                        {
                            stream.Write(frame.RawData, 0, frame.RawData.Length);
                        }
                    }
                }

                // Перекодируем временный файл mp3 в m4a.
                // Напрямую кодировать в m4b нельзя.
                // Перекодировщик требует расширение m4a.
                worker?.ReportProgress(0, "перекодировка...");
                using (var stream = new MediaFoundationReader(mergedMP3))
                {
                    MediaFoundationEncoder.EncodeToAac(stream, mergedM4A);
                }

                // Перемещаем файл временный файл m4a в целевую папку с целевым именем файла m4b.
                File.Move(mergedM4A, fileName, true);

                // Формируем и записываем тег в итоговый файл m4b.
                worker?.ReportProgress(0, "запись тега...");

                var firstTrack = fileItems.First().Track;

                Settings.MP4_createNeroChapters = true;
                Settings.MP4_createQuicktimeChapters = true;

                var track = new Track(fileName);

                track.Album = firstTrack.Album;
                track.AlbumArtist = firstTrack.AlbumArtist;
                track.Artist = firstTrack.Artist;
                track.Comment = firstTrack.Comment;
                track.Date = firstTrack.Date;
                track.Description = firstTrack.Description;
                track.Genre = firstTrack.Genre;
                track.Title = firstTrack.Album;
                track.Language = firstTrack.Language;
                track.Year = firstTrack.Year;

                // Изображения обложки.
                if (allImages)
                {
                    // Добавляем изображения из всех файлов MP3.
                    foreach (var fileItem in fileItems)
                    {
                        foreach (var picture in fileItem.Track.EmbeddedPictures)
                        {
                            track.EmbeddedPictures.Add(picture);
                        }
                    }
                }
                else
                {
                    // Добавляем изображения только из первого файла MP3.
                    foreach (var picture in firstTrack.EmbeddedPictures)
                    {
                        track.EmbeddedPictures.Add(picture);
                    }
                }

                // Содержание (разделы).
                var timestamp = new TimeSpan();
                foreach (var fileItem in fileItems)
                {
                    var chapter = new ChapterInfo
                    {
                        Title = fileItem.Track.Title,
                        StartTime = (uint)timestamp.TotalMilliseconds,
                        StartOffset = (uint)timestamp.TotalMilliseconds
                    };
                    timestamp += TimeSpan.FromMilliseconds(fileItem.Track.DurationMs);
                    chapter.EndTime = (uint)timestamp.TotalMilliseconds;
                    chapter.EndOffset = (uint)timestamp.TotalMilliseconds;
                    track.Chapters.Add(chapter);
                }

                track.Save();
                return new FileInfo(fileName);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                File.Delete(mergedMP3);
                File.Delete(mergedM4A);
            }
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
                ? Merge((string)parameters[0], (string)parameters[1], (bool)parameters[2], worker)
                : null;
        }

        private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            StatusTextBlock.Text = $"Создание файла: {e.UserState as string}";
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
            var files = folderInfo.GetFiles("*.mp3")
                .Select(x => x.FullName)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
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