using EBookStudio.Models;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using static EBookStudio.Models.ApiService;

namespace EBookStudio.Helpers
{
    public class LibraryService : ILibraryService
    {
        public Task<ApiService.UploadResult> UploadBookAsync(string f, string u)
            => ApiService.UploadBookAsync(f, u);
        public Task<byte[]> DownloadBytesAsync(string url) => ApiService.DownloadBytesAsync(url);
        public Task<bool> DownloadFileAsync(string u, string p) => ApiService.DownloadFileAsync(u, p);
        public Task<List<string>> GetMusicFileListAsync(string u, string b) => ApiService.GetMusicFileListAsync(u, b);
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
        // 실제 구현: "그래, 기존에 있던 ApiService를 불러서 진짜 서버에 보낼게"
        public Task<bool> SendCodeAsync(string email) => ApiService.SendCodeAsync(email);
        public Task<bool> VerifyCodeAsync(string e, string c) => ApiService.VerifyCodeAsync(e, c);
        public Task<string?> FindIdAsync(string email) => ApiService.FindIdAsync(email);
        public Task<bool> ResetPasswordAsync(string e, string c, string p) => ApiService.ResetPasswordAsync(e, c, p);
    }

    public class DialogService : IDialogService
    {
        public void ShowMessage(string message)
        {
            System.Windows.MessageBox.Show(message);
        }
        public bool ShowConfirm(string message, string title)
        {
            return true;
        }
    }

    public class NoteService : INoteService
    {
        public (List<NoteItem> Bookmarks, List<NoteItem> Highlights, List<NoteItem> Memos) LoadNotes(string u, string t)
        {
            var data = NoteManager.LoadNotes(u, t);
            return (data.Bookmarks, data.Highlights, data.Memos);
        }

        public void RemoveItem(string u, string t, NoteItem i)
            => NoteManager.RemoveItem(u, t, i);

        public void AddItem(string u, string t, NoteItem i)
        => NoteManager.AddItem(u, t, i);
    }

    public class BookFileSystem : IBookFileSystem
    {
        // 파일 관련
        public bool FileExists(string path) => File.Exists(path);
        public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);
        public void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }

        // 디렉토리 관련
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public string[] GetDirectories(string path) => Directory.GetDirectories(path);

        // 유저 데이터 초기화 (기존 FileHelper 활용)
        public void ResetUserData(string username) => FileHelper.ResetUserData(username);
    }

    public class AuthService : IAuthService
    {
        // 인증 관련
        public Task<bool> LoginAsync(string u, string p) => ApiService.LoginAsync(u, p);
        public Task<bool> SendVerificationCodeAsync(string e) => ApiService.SendVerificationCodeAsync(e);
        public Task<bool> VerifyCodeAsync(string e, string c) => ApiService.VerifyCodeAsync(e, c);
        public Task<bool> RegisterAsync(string u, string p, string e) => ApiService.RegisterAsync(u, p, e);

        // [추가] 서버 데이터 관리 관련
        public Task<List<ServerBookDto>> GetMyServerBooksAsync(string username)
            => ApiService.GetMyServerBooksAsync(username);

        public Task<bool> DeleteServerBookAsync(string bookTitle)
            => ApiService.DeleteServerBookAsync(bookTitle);

        public Task<bool> DownloadFileAsync(string url, string localPath)
            => ApiService.DownloadFileAsync(url, localPath);

        public Task<List<string>> GetMusicFileListAsync(string username, string bookTitle)
            => ApiService.GetMusicFileListAsync(username, bookTitle);
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

        // 인터페이스 호환성을 위한 FontSize (LineHeight와 연결하거나 별도 관리)
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
