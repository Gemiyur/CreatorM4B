using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using ATL;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace CreatorM4B;

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

    private string ResultMessage = string.Empty;

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

    private static bool Merge(string folderName, string fileName, BackgroundWorker? worker)
    {
        worker?.ReportProgress(0, "сбор данных...");
        var folder = new DirectoryInfo(folderName);
        var fileItems = folder.GetFiles("*.mp3")
            .Select(x => new FileItem(x.FullName))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        if (fileItems == null || !fileItems.Any())
        {
            return false;
        }

        var mergedMP3 = Path.GetTempFileName(); // Имя объединённого временного файла mp3.
        var mergedM4A = mergedMP3 + ".m4a";     // Имя объединённого временного файла m4a.

        try
        {
            // Создаём объединённый временный файл mp3, состоящий из всех файлов mp3 в папке.
            worker?.ReportProgress(0, "объединение файлов...");
            using (var stream = new FileStream(mergedMP3, FileMode.Create))
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

            using (var reader = new MediaFoundationReader(mergedMP3))
            {
                // Поддерживаемые скорости потока.
                // Работает только с этими параметрами.
                // Возвращает массив [96000, 128000, 160000, 192000].
                // В остальных случаях возвращает пустой массив.
                var bitrates = MediaFoundationEncoder.GetEncodeBitrates(AudioSubtypes.MFAudioFormat_AAC, 44100, 2);

                // Если массив пустой, то присваиваем ожидаемые значения.
                if (!bitrates.Any())
                {
                    bitrates = [96000, 128000, 160000, 192000];
                }

                // Наибольшая скорость потока среди файлов mp3.
                var bitrateMP3 = fileItems.Max(x => x.Track.Bitrate) * 1000;

                // Определяем скорость потока для перекодировки.
                int bitrate;
                if (bitrates.Any(x => x == bitrateMP3))
                    bitrate = bitrateMP3;
                else if (bitrateMP3 > bitrates.Last())
                    bitrate = bitrates.Last();
                else if (bitrateMP3 < bitrates.First())
                    bitrate = bitrates.First();
                else
                    bitrate = bitrates.FirstOrDefault(x => x > bitrateMP3);

                // Перекодируем с определённой скоростью потока.
                MediaFoundationEncoder.EncodeToAac(reader, mergedM4A, bitrate);
            }

            // Перемещаем файл временный файл m4a в целевую папку с целевым именем файла m4b.
            File.Move(mergedM4A, fileName, true);

            // Формируем и записываем тег в итоговый файл m4b.
            worker?.ReportProgress(0, "запись тега...");

            var firstTrack = fileItems.First().Track;

            Settings.MP4_createNeroChapters = true;
            Settings.MP4_createQuicktimeChapters = true;

            var track = new Track(fileName)
            {
                Album = firstTrack.Album,
                AlbumArtist = firstTrack.AlbumArtist,
                Artist = firstTrack.Artist,
                Comment = firstTrack.Comment,
                Date = firstTrack.Date,
                Description = firstTrack.Description,
                Genre = firstTrack.Genre,
                Title = firstTrack.Album,
                Language = firstTrack.Language,
                Year = firstTrack.Year
            };

            // Добавляем только уникальные изображения из всех файлов MP3.
            foreach (var fileItem in fileItems)
            {
                foreach (var picture in fileItem.Track.EmbeddedPictures)
                {
                    if (track.EmbeddedPictures.Any(x => x.PictureHash == picture.PictureHash))
                        continue;
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
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            File.Delete(mergedMP3);
            File.Delete(mergedM4A);
        }
    }

    private void CheckCreateButton()
    {
        CreateButton.IsEnabled = FolderTextBox.Text.Any() && FileTextBox.Text.Any();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (Worker.IsBusy == true)
        {
            MessageBox.Show("Нельзя закрывать приложение, пока идёт создание файла.", Title);
            e.Cancel = true;
        }
    }

    private void Worker_DoWork(object? sender, DoWorkEventArgs e)
    {
        var parameters = (object[]?)e.Argument;
        BackgroundWorker? worker = sender as BackgroundWorker;
        e.Result = parameters != null
            ? Merge((string)parameters[0], (string)parameters[1], worker)
            : false;
    }

    private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
    {
        StatusTextBlock.Text = $"Создание файла: {e.UserState as string}";
    }

    private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            ResultMessage = e.Error.ToString();
            StatusTextBlock.Text = "Ошибка";
        }
        else
        {
            ResultMessage = "Файл успешно создан.";
            StatusTextBlock.Text = "Готово";
        }
        // Пусть пока остаётся на всякий случай.
        //var result = (bool?)e.Result == true;
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
            ResultMessage = string.Empty;
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
        object[] parameters = [FolderTextBox.Text, FileTextBox.Text];
        Worker.RunWorkerAsync(parameters);
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        new AboutDialog() { Owner = this }.ShowDialog();
    }

    private void StatusTextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultMessage != string.Empty)
            MessageBox.Show(ResultMessage, Title);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}