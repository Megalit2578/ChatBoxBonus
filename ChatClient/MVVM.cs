using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ChatClient
{
    public abstract class MessageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SystemMessageViewModel : MessageViewModel
    {
        public string Text { get; set; } = string.Empty;
    }

    public class TextMessageViewModel : MessageViewModel
    {
        public string Sender { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public Brush AvatarColor { get; set; } = Brushes.Gray;
        public string Initials { get; set; } = string.Empty;
        public bool IsMine { get; set; }
        public bool IsFirstInGroup { get; set; }
        
        private ImageSource? _avatarImage;
        public ImageSource? AvatarImage 
        { 
            get => _avatarImage; 
            set { _avatarImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(AvatarInitialsVisibility)); OnPropertyChanged(nameof(AvatarImageVisibility)); } 
        }

        public Visibility AvatarVisibility => (!IsMine && IsFirstInGroup) ? Visibility.Visible : Visibility.Hidden;
        public Visibility AvatarInitialsVisibility => _avatarImage == null ? Visibility.Visible : Visibility.Hidden;
        public Visibility AvatarImageVisibility => _avatarImage != null ? Visibility.Visible : Visibility.Hidden;
        
        public Visibility HeaderVisibility => (!IsMine && IsFirstInGroup) ? Visibility.Visible : Visibility.Collapsed;
        public Thickness Margin => IsFirstInGroup ? new Thickness(10, 15, 10, 0) : new Thickness(10, 2, 10, 0);

        public HorizontalAlignment Alignment => IsMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        public Brush MessageBackground => IsMine ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30"));
        public CornerRadius BubbleRadius => IsMine ? new CornerRadius(12, 12, 2, 12) : new CornerRadius(12, 12, 12, 2);
    }

    public class FileMessageViewModel : MessageViewModel
    {
        public string Sender { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileSizeStr { get; set; } = string.Empty;
        public string FileId { get; set; } = string.Empty;
        
        public bool IsMine { get; set; }
        public HorizontalAlignment Alignment => IsMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        public Thickness Margin => IsMine ? new Thickness(20, 8, 20, 15) : new Thickness(60, 8, 20, 15);
        public Brush MessageBackground => IsMine ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#005A9E")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28282B"));
        
        private string _statusText = string.Empty;
        public string StatusText 
        { 
            get => _statusText; 
            set { _statusText = value; OnPropertyChanged(); } 
        }
        
        private Brush _statusColor = Brushes.Gray;
        public Brush StatusColor 
        { 
            get => _statusColor; 
            set { _statusColor = value; OnPropertyChanged(); } 
        }

        private bool _canDownload = true;
        public bool CanDownload 
        { 
            get => _canDownload; 
            set { _canDownload = value; OnPropertyChanged(); } 
        }

        public ICommand? DownloadCommand { get; set; }

        private ImageSource? _imageContent;
        public ImageSource? ImageContent 
        {
            get => _imageContent;
            set 
            { 
                _imageContent = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(ImageVisibility));
            }
        }

        public Visibility ImageVisibility => _imageContent != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) { _execute = execute; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        
        // Suppress unused event warning
        protected virtual void OnCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
