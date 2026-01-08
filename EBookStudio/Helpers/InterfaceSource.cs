using EBookStudio.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace EBookStudio.Helpers
{
    public interface IDialogService
    {
        void ShowMessage(string message);
        bool ShowConfirm(string message, string title);
    }

    public interface ILibraryService
    {
        Task<UploadResult> UploadBookAsync(string filePath, string username);
        Task<byte[]> DownloadBytesAsync(string url);
        Task<bool> DownloadFileAsync(string url, string localPath);
        // [수정] bookId -> bookFolder
        Task<List<string>> GetMusicFileListAsync(string username, string bookFolder);
    }

    public interface IFilePickerService
    {
        string? PickPdfFile();
    }

    public interface IAccountService
    {
        Task<bool> SendCodeAsync(string email);
        Task<bool> VerifyCodeAsync(string email, string code);
        Task<string?> FindIdAsync(string email);
        Task<bool> ResetPasswordAsync(string email, string code, string newPassword);
    }

    public interface ISettingsService
    {
        FontFamily FontFamily { get; set; }
        double LineHeight { get; set; }
        double FontSize { get; set; }
        Brush Background { get; set; }
        Brush Foreground { get; set; }

        void ApplyLightMode();
        void ApplySepiaMode();
        void ApplyDarkMode();
    }

    public interface INoteService
    {
        // [수정] bookTitle -> bookFolder
        (List<NoteItem> Bookmarks, List<NoteItem> Highlights, List<NoteItem> Memos) LoadNotes(string username, string bookFolder);
        void RemoveItem(string username, string bookFolder, NoteItem item);
        void AddItem(string username, string bookFolder, NoteItem item);
    }

    public interface IBookFileSystem
    {
        bool FileExists(string path);
        Task<string> ReadAllTextAsync(string path);
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        string[] GetDirectories(string path);
        void DeleteFile(string path);
        void ResetUserData(string username);
    }

    public interface IAuthService
    {
        Task<bool> LoginAsync(string username, string password);
        Task<bool> SendVerificationCodeAsync(string email);
        Task<bool> VerifyCodeAsync(string email, string code);
        Task<bool> RegisterAsync(string username, string password, string email);
        Task<List<ServerBook>> GetMyServerBooksAsync(string username);
        Task<bool> DeleteServerBookAsync(string bookFolder); // [수정]
        Task<bool> DownloadFileAsync(string url, string localPath);
        Task<List<string>> GetMusicFileListAsync(string username, string bookFolder); // [수정]
    }

    public interface INetworkService
    {
        Task<bool> CheckInternetConnectionAsync();
    }

    public interface IApiService
    {
        Task<bool> RegisterAsync(string username, string password, string email, string code);
        Task<bool> LoginAsync(string username, string password);
        void Logout();
        Task<bool> SendCodeAsync(string email);
        Task<bool> VerifyCodeAsync(string email, string code);
        Task<string?> FindIdAsync(string email);
        Task<bool> ResetPasswordAsync(string email, string code, string newPassword);
        Task<UploadResult> UploadBookAsync(string filePath, string username);
        Task<bool> DownloadFileAsync(string url, string localPath);
        Task<byte[]> DownloadBytesAsync(string url);
        Task<List<string>> GetMusicFileListAsync(string username, string bookFolder); // [수정]
        Task<List<ServerBook>> GetMyServerBooksAsync(string username);
        Task<bool> DeleteServerBookAsync(string bookFolder); // [수정]
    }
}