using EBookStudio.Models;
using EBookStudio.Services;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace EBookStudio.Helpers
{
    public class LibraryService : ILibraryService
    {
        private readonly IApiService _apiService;

        public LibraryService(IApiService? apiService = null)
        {
            _apiService = apiService ?? new ApiService();
        }

        public async Task<UploadResult> UploadBookAsync(string filePath, string username)
        {
            return await _apiService.UploadBookAsync(filePath, username);
        }

        public async Task<byte[]> DownloadBytesAsync(string url)
        {
            return await _apiService.DownloadBytesAsync(url);
        }

        public async Task<bool> DownloadFileAsync(string url, string localPath)
        {
            return await _apiService.DownloadFileAsync(url, localPath);
        }

        public async Task<List<string>> GetMusicFileListAsync(string username, string bookFolder)
        {
            return await _apiService.GetMusicFileListAsync(username, bookFolder);
        }
    }

    public class FilePickerService : IFilePickerService
    {
        public string? PickPdfFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF 문서 (*.pdf)|*.pdf",
                Title = "책 선택"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }

    public class AccountService : IAccountService
    {
        private readonly IApiService _apiService;

        public AccountService(IApiService? apiService = null)
        {
            _apiService = apiService ?? new ApiService();
        }

        public async Task<bool> SendCodeAsync(string email)
        {
            return await _apiService.SendCodeAsync(email);
        }

        public async Task<bool> VerifyCodeAsync(string email, string code)
        {
            return await _apiService.VerifyCodeAsync(email, code);
        }

        public async Task<string?> FindIdAsync(string email)
        {
            return await _apiService.FindIdAsync(email);
        }

        public async Task<bool> ResetPasswordAsync(string email, string code, string newPassword)
        {
            return await _apiService.ResetPasswordAsync(email, code, newPassword);
        }
    }

    public class DialogService : IDialogService
    {
        public void ShowMessage(string message)
        {
            System.Windows.MessageBox.Show(message);
        }
        public bool ShowConfirm(string message, string title)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
    }

    public class NoteService : INoteService
    {
        // [수정] bookTitle -> bookFolder (로컬 파일 저장을 위해)
        public (List<NoteItem> Bookmarks, List<NoteItem> Highlights, List<NoteItem> Memos) LoadNotes(string u, string f)
        {
            var data = NoteManager.LoadNotes(u, f);
            return (data.Bookmarks, data.Highlights, data.Memos);
        }

        public void RemoveItem(string u, string f, NoteItem i)
            => NoteManager.RemoveItem(u, f, i);

        public void AddItem(string u, string f, NoteItem i)
            => NoteManager.AddItem(u, f, i);
    }

    public class BookFileSystem : IBookFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);
        public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);
        public void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }

        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public string[] GetDirectories(string path) => Directory.GetDirectories(path);

        public void ResetUserData(string username) => FileHelper.ResetUserData(username);
    }

    public class AuthService : IAuthService
    {
        private readonly IApiService _apiService;

        public AuthService(IApiService? apiService = null)
        {
            _apiService = apiService ?? new ApiService();
        }

        public async Task<bool> LoginAsync(string u, string p)
            => await _apiService.LoginAsync(u, p);

        public async Task<bool> SendVerificationCodeAsync(string e)
            => await _apiService.SendCodeAsync(e);

        public async Task<bool> VerifyCodeAsync(string e, string c)
            => await _apiService.VerifyCodeAsync(e, c);

        public async Task<bool> RegisterAsync(string u, string p, string e, string c)
        {
            return await _apiService.RegisterAsync(u, p, e, c);
        }

        public async Task<List<ServerBook>> GetMyServerBooksAsync(string username)
            => await _apiService.GetMyServerBooksAsync(username);

        public async Task<bool> DeleteServerBookAsync(string bookFolder)
            => await _apiService.DeleteServerBookAsync(bookFolder);

        public async Task<bool> DownloadFileAsync(string url, string localPath)
            => await _apiService.DownloadFileAsync(url, localPath);

        public async Task<List<string>> GetMusicFileListAsync(string username, string bookFolder)
            => await _apiService.GetMusicFileListAsync(username, bookFolder);
    }

    public class NetworkService : INetworkService
    {
        public Task<bool> CheckInternetConnectionAsync() => NetworkHelper.CheckInternetConnectionAsync();
    }

    public class SettingsService : INotifyPropertyChanged, ISettingsService
    {
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private SettingsService() { }

        private FontFamily _fontFamily = new FontFamily("Malgun Gothic");
        public FontFamily FontFamily
        {
            get => _fontFamily;
            set { _fontFamily = value; OnPropertyChanged(); }
        }

        private double _lineHeight = 40;
        public double LineHeight
        {
            get => _lineHeight;
            set { _lineHeight = value; OnPropertyChanged(); }
        }

        public double FontSize
        {
            get => LineHeight;
            set => LineHeight = value;
        }

        private Brush _background = Brushes.White;
        public Brush Background
        {
            get => _background;
            set { _background = value; OnPropertyChanged(); }
        }

        private Brush _foreground = new SolidColorBrush(Color.FromRgb(34, 34, 34));
        public Brush Foreground
        {
            get => _foreground;
            set { _foreground = value; OnPropertyChanged(); }
        }

        public void ApplyLightMode()
        {
            Background = Brushes.White;
            Foreground = new SolidColorBrush(Color.FromRgb(34, 34, 34));
        }

        public void ApplySepiaMode()
        {
            Background = new SolidColorBrush(Color.FromRgb(244, 236, 216));
            Foreground = new SolidColorBrush(Color.FromRgb(95, 75, 50));
        }

        public void ApplyDarkMode()
        {
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}