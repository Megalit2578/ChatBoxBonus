using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        private TcpClient? _client;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private bool _isConnected = false;
        private string _userName = "User";
        private string _serverIP = "127.0.0.1";
        private readonly int _serverPort = 5000;
        private string _lastSender = "";
        private System.Collections.Generic.Dictionary<string, ImageSource> _userAvatars = new System.Collections.Generic.Dictionary<string, ImageSource>();

        // MVVM Collections
        public ObservableCollection<MessageViewModel> Messages { get; set; } = new ObservableCollection<MessageViewModel>();
        public ObservableCollection<string> Emojis { get; set; } = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            PopulateEmojis();
            
            // Scroll to bottom automatically
            Messages.CollectionChanged += (s, e) => 
            {
                if (VisualTreeHelper.GetChildrenCount(chatItemsControl) > 0)
                {
                    var border = VisualTreeHelper.GetChild(chatItemsControl, 0) as Decorator;
                    if (border?.Child is System.Windows.Controls.ScrollViewer scrollViewer)
                    {
                        scrollViewer.ScrollToEnd();
                    }
                }
            };
        }

        private void PopulateEmojis()
        {
            string[] emojis = { "😀", "😂", "🤣", "😊", "😍", "😘", "😜", "😎", "🥺", "😭", "😡", "👍", "👎", "👏", "🙏", "❤️", "✨", "🔥", "💯" };
            foreach (var e in emojis) Emojis.Add(e);
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                Disconnect();
                return;
            }

            _userName = txtUsername.Text.Trim();
            if (string.IsNullOrEmpty(_userName))
            {
                MessageBox.Show("Please enter a username.");
                return;
            }

            _serverIP = txtServerIP.Text.Trim();
            if (string.IsNullOrEmpty(_serverIP)) _serverIP = "127.0.0.1";

            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_serverIP, _serverPort);
                
                NetworkStream stream = _client.GetStream();
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                _reader = new StreamReader(stream, new UTF8Encoding(false), leaveOpen: true);

                await _writer.WriteLineAsync($"USER:{_userName}");

                _isConnected = true;
                btnConnect.Content = "Disconnect";
                btnConnect.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123")); // Modern Red
                
                txtUsername.IsEnabled = false;
                txtServerIP.IsEnabled = false;
                txtMessage.IsEnabled = true;
                btnSend.IsEnabled = true;

                // Fake Profile Update for UI
                lblProfileName.Text = _userName;
                lblProfileStatus.Text = "Connected";
                lblProfileStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10893E"));
                lblAvatarInitials.Text = _userName.Substring(0, 1).ToUpper();
                profileAvatar.Background = GetAvatarColor(_userName);

                AppendSystemMessage("Connected to server successfully.");

                _ = Task.Run(ListenForMessagesAsync);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect: {ex.Message}");
            }
        }

        private async Task ListenForMessagesAsync()
        {
            try
            {
                while (_isConnected && _client!.Connected)
                {
                    string? message = await _reader!.ReadLineAsync();
                    if (message == null) break;

                    if (message.StartsWith("MSG:"))
                    {
                        string[] parts = message.Split(new[] { ':' }, 3);
                        if (parts.Length == 3)
                        {
                            string sender = parts[1];
                            string text = parts[2];
                            if (text.StartsWith("[AVATAR]"))
                            {
                                string base64 = text.Substring(8);
                                Dispatcher.Invoke(() => UpdateUserAvatar(sender, base64));
                            }
                            else
                            {
                                Dispatcher.Invoke(() => AppendChatMessage(sender, text));
                            }
                        }
                    }
                    else if (message.StartsWith("FILE_OFFER:"))
                    {
                        string[] parts = message.Split(new[] { ':' }, 5);
                        if (parts.Length == 5)
                        {
                            Dispatcher.Invoke(() => AppendFileOffer(parts[1], parts[2], long.Parse(parts[3]), parts[4]));
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(() => AppendSystemMessage(message));
                    }
                }
            }
            catch { }
            finally
            {
                Dispatcher.Invoke(Disconnect);
            }
        }

        private void Disconnect()
        {
            if (!_isConnected) return;

            _isConnected = false;
            _client?.Close();
            _lastSender = "";
            
            btnConnect.Content = "Connect";
            btnConnect.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            
            txtUsername.IsEnabled = true;
            txtServerIP.IsEnabled = true;
            txtMessage.IsEnabled = false;
            btnSend.IsEnabled = false;

            lblProfileName.Text = "Offline";
            lblProfileStatus.Text = "Not Connected";
            lblProfileStatus.Foreground = (Brush)FindResource("TextMuted");
            lblAvatarInitials.Text = "U";
            lblAvatarInitials.Visibility = Visibility.Visible;
            imgProfileAvatar.Visibility = Visibility.Hidden;
            brushProfileAvatar.ImageSource = null;
            profileAvatar.Background = (Brush)FindResource("AccentColor");

            AppendSystemMessage("Disconnected from server.");
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e) => await SendMessageAsync();

        private async void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await SendMessageAsync();
        }

        private async Task SendMessageAsync()
        {
            if (!_isConnected) return;

            string message = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            try
            {
                await _writer!.WriteLineAsync(message);
                txtMessage.Text = "";
                txtMessage.Focus();
            }
            catch
            {
                Disconnect();
            }
        }

        private void btnEmoji_Click(object sender, RoutedEventArgs e)
        {
            emojiPopup.IsOpen = !emojiPopup.IsOpen;
        }

        private void EmojiList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is string emoji)
            {
                int caretIndex = txtMessage.CaretIndex;
                txtMessage.Text = txtMessage.Text.Insert(caretIndex, emoji);
                txtMessage.CaretIndex = caretIndex + emoji.Length;
                txtMessage.Focus();
                ((System.Windows.Controls.ListBox)sender).SelectedItem = null;
                emojiPopup.IsOpen = false; // Hide after picking
            }
        }

        private async void profileAvatar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isConnected) return;
            OpenFileDialog openFileDialog = new OpenFileDialog { Title = "Select Avatar Image", Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif" };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.DecodePixelWidth = 100; // Resize to limit base64 string length
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    encoder.QualityLevel = 75;
                    
                    using var ms = new MemoryStream();
                    encoder.Save(ms);
                    string base64 = Convert.ToBase64String(ms.ToArray());
                    
                    await _writer!.WriteLineAsync($"[AVATAR]{base64}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to set avatar: {ex.Message}");
                }
            }
        }

        private void UpdateUserAvatar(string sender, string base64)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(imageBytes);
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze(); // Allow crossing threads if needed

                _userAvatars[sender] = bitmap;

                if (sender == _userName)
                {
                    brushProfileAvatar.ImageSource = bitmap;
                    imgProfileAvatar.Visibility = Visibility.Visible;
                    lblAvatarInitials.Visibility = Visibility.Hidden;
                }

                // Update past messages in the UI
                foreach (var msg in Messages)
                {
                    if (msg is TextMessageViewModel tvm && tvm.Sender == sender)
                    {
                        tvm.AvatarImage = bitmap;
                    }
                }
            }
            catch { }
        }

        private async void btnAttach_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Title = "Select File to Send" };
            if (openFileDialog.ShowDialog() == true)
            {
                await UploadFileAsync(openFileDialog.FileName);
            }
        }

        private async void btnAttachFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new OpenFolderDialog { Title = "Select Folder to Send" };
            if (openFolderDialog.ShowDialog() == true)
            {
                await UploadFolderAsync(openFolderDialog.FolderName);
            }
        }

        private async Task UploadFolderAsync(string folderPath)
        {
            try
            {
                string folderName = new DirectoryInfo(folderPath).Name;
                string tempZipPath = Path.Combine(Path.GetTempPath(), $"{folderName}_{Guid.NewGuid()}.zip");

                AppendSystemMessage($"Compressing folder '{folderName}'...");
                await Task.Run(() => ZipFile.CreateFromDirectory(folderPath, tempZipPath, CompressionLevel.NoCompression, false));
                await UploadFileAsync(tempZipPath, originalName: folderName + ".zip", deleteAfter: true);
            }
            catch (Exception ex)
            {
                AppendSystemMessage($"Failed to process folder: {ex.Message}");
            }
        }

        private async Task UploadFileAsync(string filePath, string? originalName = null, bool deleteAfter = false)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                string fileName = originalName ?? fileInfo.Name;
                long fileSize = fileInfo.Length;

                AppendSystemMessage($"Uploading {fileName} ({FormatBytes(fileSize)})...");

                TcpClient uploadClient = new TcpClient();
                await uploadClient.ConnectAsync(_serverIP, _serverPort);

                using NetworkStream stream = uploadClient.GetStream();
                byte[] headerBytes = Encoding.UTF8.GetBytes($"FILE_UPLOAD:{_userName}:{fileName}:{fileSize}\n");
                await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                {
                    await fs.CopyToAsync(stream);
                    await stream.FlushAsync();
                }

                uploadClient.Close();
                AppendSystemMessage($"Uploaded {fileName} successfully.");
            }
            catch (Exception ex)
            {
                AppendSystemMessage($"Upload failed: {ex.Message}");
            }
            finally
            {
                if (deleteAfter && File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }
            }
        }

        private async Task DownloadFileAsync(FileMessageViewModel vm)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog { FileName = vm.FileName };
            if (saveFileDialog.ShowDialog() != true) return;

            string savePath = saveFileDialog.FileName;
            vm.CanDownload = false;
            vm.StatusText = "Downloading...";

            try
            {
                TcpClient downloadClient = new TcpClient();
                await downloadClient.ConnectAsync(_serverIP, _serverPort);

                using NetworkStream stream = downloadClient.GetStream();
                byte[] headerBytes = Encoding.UTF8.GetBytes($"FILE_DOWNLOAD:{vm.FileId}\n");
                await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

                using (FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await stream.CopyToAsync(fs);
                }

                downloadClient.Close();
                vm.StatusText = "Downloaded";
                vm.StatusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10893E"));
            }
            catch (Exception ex)
            {
                vm.StatusText = "Failed";
                vm.StatusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123"));
                MessageBox.Show($"Download failed: {ex.Message}");
                vm.CanDownload = true;
            }
        }

        private void AppendSystemMessage(string text)
        {
            _lastSender = "";
            Messages.Add(new SystemMessageViewModel { Text = text });
        }

        private void AppendChatMessage(string sender, string message)
        {
            bool isFirst = sender != _lastSender;
            _lastSender = sender;

            _userAvatars.TryGetValue(sender, out ImageSource? avatarImage);

            Messages.Add(new TextMessageViewModel
            {
                Sender = sender,
                Text = message,
                Time = DateTime.Now.ToString("HH:mm"),
                AvatarColor = GetAvatarColor(sender),
                Initials = sender.Length > 0 ? sender.Substring(0, 1).ToUpper() : "U",
                IsFirstInGroup = isFirst,
                AvatarImage = avatarImage
            });
        }

        private void AppendFileOffer(string sender, string fileName, long fileSize, string fileId)
        {
            _lastSender = "";
            var vm = new FileMessageViewModel
            {
                Sender = sender,
                FileName = fileName,
                FileSizeStr = FormatBytes(fileSize),
                FileId = fileId
            };
            vm.DownloadCommand = new RelayCommand(async () => await DownloadFileAsync(vm));
            Messages.Add(vm);
        }

        private static SolidColorBrush GetAvatarColor(string name)
        {
            int hash = name.GetHashCode();
            byte r = (byte)((hash & 0xFF0000) >> 16);
            byte g = (byte)((hash & 0x00FF00) >> 8);
            byte b = (byte)(hash & 0x0000FF);
            // Copilot/Fluent inspired vibrant pastels
            r = (byte)((r % 128) + 100);
            g = (byte)((g % 128) + 100);
            b = (byte)((b % 128) + 150);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}