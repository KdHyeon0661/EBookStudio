using EBookStudio.Models;
using EBookStudio.Helpers;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace EBookStudio.Services
{
    public class LibraryService : ILibraryService
    {
        private readonly IApiService _apiService;

        public LibraryService(IApiService? apiService = null)
        {
            _apiService = apiService ?? new ApiService();
        }

        public async Task<UploadResult> UploadBookAsync(string filePath, string username, string requestId)
        {
            return await _apiService.UploadBookAsync(filePath, username, requestId);
        }

        public async Task<ApiResult<byte[]>> DownloadBytesAsync(string url)
        {
            return await _apiService.DownloadBytesAsync(url);
        }

        public async Task<ApiResult> DownloadFileAsync(string url, string localPath,
                                                       CancellationToken cancellationToken = default)
        {
            return await _apiService.DownloadFileAsync(url, localPath, cancellationToken);
        }

        public async Task<ApiResult<List<string>>> GetMusicFileListAsync(string username, string bookFolder)
        {
            return await _apiService.GetMusicFileListAsync(username, bookFolder);
        }

        public Task<ApiResult<JobStatusResponse>> GetJobStatusAsync(string jobId)
            => _apiService.GetJobStatusAsync(jobId);

        public Task<ApiResult<JobStatusResponse>> CancelJobAsync(string jobId)
            => _apiService.CancelJobAsync(jobId);
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

        public async Task<CodeSendResult> SendCodeAsync(string email)
        {
            return await _apiService.SendCodeAsync(email, "recovery");
        }

        public async Task<ApiResult> VerifyCodeAsync(string email, string code)
        {
            return await _apiService.VerifyCodeAsync(email, code, "recovery");
        }

        public async Task<ApiResult<string>> FindIdAsync(string email, string code)
        {
            return await _apiService.FindIdAsync(email, code);
        }

        public async Task<ApiResult> ResetPasswordAsync(string email, string code, string newPassword)
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

        public void SaveNotes(string u, string f, BookNoteData data)
            => NoteManager.SaveNotes(u, f, data);

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
        public void ResetLocalLibrary(string cacheKey) => FileHelper.ResetLocalLibrary(cacheKey);
    }

    public class AuthService : IAuthService
    {
        private readonly IApiService _apiService;

        public AuthService(IApiService? apiService = null)
        {
            _apiService = apiService ?? new ApiService();
        }

        public Task<ApiResult<LocalAccount>> LoginAsync(string u, string p, CancellationToken cancellationToken = default)
            => _apiService.LoginAsync(u, p, cancellationToken);

        public Task LogoutAsync() => _apiService.LogoutAsync();

        public async Task<CodeSendResult> SendVerificationCodeAsync(string e)
            => await _apiService.SendCodeAsync(e, "register");

        public async Task<ApiResult> VerifyCodeAsync(string e, string c)
            => await _apiService.VerifyCodeAsync(e, c, "register");

        public async Task<ApiResult> RegisterAsync(string u, string p, string e, string c)
        {
            return await _apiService.RegisterAsync(u, p, e, c);
        }

        public async Task<ApiResult<List<ServerBook>>> GetMyServerBooksAsync(string username)
            => await _apiService.GetMyServerBooksAsync(username);

        public async Task<ApiResult> DeleteServerBookAsync(string bookFolder)
            => await _apiService.DeleteServerBookAsync(bookFolder);

        public Task<ApiResult<byte[]>> DownloadBytesAsync(string url)
            => _apiService.DownloadBytesAsync(url);

        public Task<ApiResult> DownloadFileAsync(string url, string localPath,
                                                 CancellationToken cancellationToken = default)
            => _apiService.DownloadFileAsync(url, localPath, cancellationToken);

        public async Task<ApiResult<List<string>>> GetMusicFileListAsync(string username, string bookFolder)
            => await _apiService.GetMusicFileListAsync(username, bookFolder);

        public Task<ApiResult> ChangePasswordAsync(string currentPassword, string newPassword)
            => _apiService.ChangePasswordAsync(currentPassword, newPassword);

        public Task<ApiResult> DeleteAccountAsync() => _apiService.DeleteAccountAsync();
    }

    public class SettingsService : INotifyPropertyChanged, ISettingsService
    {
        private static readonly Brush DefaultForeground =
            ShareableBrush(new SolidColorBrush(Color.FromRgb(34, 34, 34)));
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private bool _loading;

        private SettingsService()
        {
            Load();
        }

        private FontFamily _fontFamily = new FontFamily("Malgun Gothic");
        public FontFamily FontFamily
        {
            get => _fontFamily;
            set
            {
                if (value == null || _fontFamily.Source == value.Source) return;
                _fontFamily = value;
                OnPropertyChanged();
                Save();
            }
        }

        private double _lineHeight = 40;
        public double LineHeight
        {
            get => _lineHeight;
            set
            {
                double normalized = Math.Clamp(value, 20, 100);
                if (Math.Abs(_lineHeight - normalized) < 0.01) return;
                _lineHeight = normalized;
                OnPropertyChanged();
                Save();
            }
        }

        private double _fontSize = 22;
        public double FontSize
        {
            get => _fontSize;
            set
            {
                double normalized = Math.Clamp(value, 12, 72);
                if (Math.Abs(_fontSize - normalized) < 0.01) return;
                _fontSize = normalized;
                OnPropertyChanged();
                Save();
            }
        }

        private Brush _background = Brushes.White;
        public Brush Background
        {
            get => _background;
            set
            {
                if (BrushText(_background) == BrushText(value)) return;
                _background = ShareableBrush(value);
                OnPropertyChanged();
                Save();
            }
        }

        private Brush _foreground = DefaultForeground;
        public Brush Foreground
        {
            get => _foreground;
            set
            {
                if (BrushText(_foreground) == BrushText(value)) return;
                _foreground = ShareableBrush(value);
                OnPropertyChanged();
                Save();
            }
        }

        public void ApplyLightMode()
        {
            Background = Brushes.White;
            Foreground = DefaultForeground;
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

        private void Load()
        {
            string path = SettingsPath();
            if (!File.Exists(path)) return;
            try
            {
                SettingsState? state = JsonSerializer.Deserialize<SettingsState>(File.ReadAllText(path));
                if (state == null) return;
                _loading = true;
                if (!string.IsNullOrWhiteSpace(state.FontFamily))
                    _fontFamily = new FontFamily(state.FontFamily);
                _fontSize = Math.Clamp(state.FontSize, 12, 72);
                _lineHeight = Math.Clamp(state.LineHeight, 20, 100);
                _background = ParseBrush(state.Background, Brushes.White);
                _foreground = ParseBrush(state.Foreground, DefaultForeground);
            }
            catch (Exception error) when (error is IOException or JsonException
                                          or NotSupportedException or FormatException)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings load] {error}");
            }
            finally
            {
                _loading = false;
            }
        }

        private void Save()
        {
            if (_loading) return;
            try
            {
                var state = new SettingsState
                {
                    FontFamily = FontFamily.Source,
                    FontSize = FontSize,
                    LineHeight = LineHeight,
                    Background = BrushText(Background),
                    Foreground = BrushText(Foreground)
                };
                AtomicFile.WriteAllText(SettingsPath(),
                    JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings save] {error}");
            }
        }

        private static string SettingsPath() => Path.Combine(FileHelper.BasePath, "settings.json");

        private static string BrushText(Brush? brush)
            => brush is SolidColorBrush solid ? solid.Color.ToString() : brush?.ToString() ?? "#FFFFFFFF";

        private static Brush ParseBrush(string? text, Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            var converter = new BrushConverter();
            return converter.ConvertFromInvariantString(text) is Brush brush
                ? ShareableBrush(brush) : fallback;
        }

        // Frozen theme brushes can be shared across WPF dispatchers.
        private static Brush ShareableBrush(Brush brush)
            => brush.IsFrozen || !brush.CanFreeze ? brush : (Brush)brush.GetCurrentValueAsFrozen();

        private sealed class SettingsState
        {
            public string FontFamily { get; set; } = "Malgun Gothic";
            public double FontSize { get; set; } = 22;
            public double LineHeight { get; set; } = 40;
            public string Background { get; set; } = "#FFFFFFFF";
            public string Foreground { get; set; } = "#FF222222";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
