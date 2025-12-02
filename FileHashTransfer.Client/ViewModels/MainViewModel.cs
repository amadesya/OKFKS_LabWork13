using FileHashTransfer.Common;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FileHashTransfer.Client.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private string _filePath = "";
        private string _fileName = "Не выбран";
        private long _fileSize;
        private string _hash = "Хэш: -";
        private string _salt = "Соль: -";
        private string _status = "Готов к работе...";
        private double _progress;
        private bool _useSalt = true;
        private bool _isSending;

        public MainViewModel()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:8080/"),
                Timeout = TimeSpan.FromMinutes(5)
            };

            BrowseCommand = new RelayCommand(BrowseFile);
            SendCommand = new RelayCommand(async () => await SendFileAsync(), () => CanSendFile);
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged();
                    if (!string.IsNullOrEmpty(value) && File.Exists(value))
                    {
                        UpdateFileInfo();
                    }
                }
            }
        }

        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged();
            }
        }

        public long FileSize
        {
            get => _fileSize;
            set
            {
                _fileSize = value;
                OnPropertyChanged();
            }
        }

        public string Hash
        {
            get => _hash;
            set
            {
                _hash = value;
                OnPropertyChanged();
            }
        }

        public string Salt
        {
            get => _salt;
            set
            {
                _salt = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged();
            }
        }

        public bool UseSalt
        {
            get => _useSalt;
            set
            {
                _useSalt = value;
                OnPropertyChanged();
                if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                {
                    UpdateFileInfo();
                }
            }
        }

        public bool IsSending
        {
            get => _isSending;
            set
            {
                _isSending = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool CanSendFile => !IsSending && !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

        public RelayCommand BrowseCommand { get; }
        public RelayCommand SendCommand { get; }

        private void BrowseFile()
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                FilePath = dialog.FileName;
            }
        }

        private void UpdateFileInfo()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
                return;

            try
            {
                var fileInfo = new FileInfo(FilePath);
                FileName = fileInfo.Name;
                FileSize = fileInfo.Length;

                byte[] fileBytes = File.ReadAllBytes(FilePath);

                if (UseSalt)
                {
                    var result = HashCalculator.ComputeHashWithSalt(fileBytes);
                    Hash = $"Хэш: {result.hash}";
                    Salt = $"Соль: {Convert.ToBase64String(result.salt)[..16]}...";
                }
                else
                {
                    Hash = $"Хэш: {HashCalculator.ComputeHash(fileBytes)}";
                    Salt = "Соль: не используется";
                }

                Status = "Файл готов к отправке";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка чтения файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SendFileAsync()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))
            {
                MessageBox.Show("Выберите файл для отправки", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSending = true;
            Status = "Подготовка файла...";
            Progress = 10;

            try
            {
                // Чтение файла
                byte[] fileBytes = File.ReadAllBytes(FilePath);
                string fileName = Path.GetFileName(FilePath);

                Progress = 30;

                // Вычисление хэша
                string hash;
                byte[] salt;

                if (UseSalt)
                {
                    var result = HashCalculator.ComputeHashWithSalt(fileBytes);
                    hash = result.hash;
                    salt = result.salt;
                }
                else
                {
                    hash = HashCalculator.ComputeHash(fileBytes);
                    salt = Array.Empty<byte>();
                }

                Progress = 50;

                // Подготовка данных для отправки
                var fileData = new FileTransferData
                {
                    FileName = fileName,
                    FileSize = fileBytes.Length,
                    Hash = hash,
                    Salt = salt,
                    FileData = fileBytes,
                    UseSalt = UseSalt
                };

                // Сериализация в JSON
                string json = JsonSerializer.Serialize(fileData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Отправка на сервер
                Status = "Отправка файла на сервер...";
                Progress = 70;

                var response = await _httpClient.PostAsync("upload", content);
                string responseText = await response.Content.ReadAsStringAsync();

                Progress = 90;

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<UploadResult>(responseText);
                    if (result != null)
                    {
                        if (result.IntegrityValid)
                        {
                            Progress = 100;
                            Status = "Файл успешно отправлен и проверен!";
                            MessageBox.Show(
                                $"Файл '{result.FileName}' успешно отправлен.\n" +
                                $"Размер: {result.FileSize} байт\n" +
                                $"Целостность: ПРОВЕРЕНА",
                                "Успех",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            Progress = 100;
                            Status = "Ошибка целостности файла!";
                            MessageBox.Show(
                                "Получатель обнаружил нарушение целостности файла!\n" +
                                "Файл был поврежден при передаче.",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    Progress = 100;
                    Status = "Ошибка отправки";
                    MessageBox.Show($"Ошибка сервера: {response.StatusCode}\n{responseText}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (HttpRequestException ex)
            {
                Progress = 100;
                Status = "Ошибка соединения";
                MessageBox.Show($"Не удалось подключиться к серверу.\nУбедитесь, что сервер запущен на localhost:8080\n\nДетали: {ex.Message}",
                    "Ошибка соединения",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Progress = 100;
                Status = "Ошибка отправки";
                MessageBox.Show($"Ошибка при отправке файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSending = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private class UploadResult
        {
            public bool Success { get; set; }
            public string FileName { get; set; } = "";
            public long FileSize { get; set; }
            public bool IntegrityValid { get; set; }
            public string Message { get; set; } = "";
        }
    }
}