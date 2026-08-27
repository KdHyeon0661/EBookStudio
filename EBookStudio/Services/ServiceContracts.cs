using EBookStudio.Models;
using System.Windows.Media;

namespace EBookStudio.Services
{
    public interface IDialogService
    {
        void ShowMessage(string message);
        bool ShowConfirm(string message, string title);
    }

    public interface ILibraryService
    {
        Task<UploadResult> UploadBookAsync(string filePath, string username, string requestId);
        Task<ApiResult<byte[]>> DownloadBytesAsync(string url);
        Task<ApiResult> DownloadFileAsync(string url, string localPath);
        Task<ApiResult<List<string>>> GetMusicFileListAsync(string username, string bookFolder);
        Task<ApiResult<JobStatusResponse>> GetJobStatusAsync(string jobId);
        Task<ApiResult<JobStatusResponse>> CancelJobAsync(string jobId);
    }

    public interface IFilePickerService
    {
        string? PickPdfFile();
    }

    public interface IAccountService
    {
        Task<CodeSendResult> SendCodeAsync(string email);
        Task<ApiResult> VerifyCodeAsync(string email, string code);
        Task<ApiResult<string>> FindIdAsync(string email, string code);
        Task<ApiResult> ResetPasswordAsync(string email, string code, string newPassword);
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
        Task<ApiResult> LoginAsync(string username, string password);
        Task LogoutAsync();
        Task<CodeSendResult> SendVerificationCodeAsync(string email);
        Task<ApiResult> VerifyCodeAsync(string email, string code);
        Task<ApiResult> RegisterAsync(string username, string password, string email, string code);
        Task<ApiResult<List<ServerBook>>> GetMyServerBooksAsync(string username);
        Task<ApiResult> DeleteServerBookAsync(string bookFolder);
        Task<ApiResult> DownloadFileAsync(string url, string localPath);
        Task<ApiResult<List<string>>> GetMusicFileListAsync(string username, string bookFolder);
        Task<ApiResult> ChangePasswordAsync(string currentPassword, string newPassword);
        Task<ApiResult> DeleteAccountAsync();
    }

    public interface IUsageService
    {
        Task<ApiResult> SyncAsync(string username);
        Task<ApiResult<UsageSummaryResponse>> GetSummaryAsync(string username);
        Task<ApiResult<UsageDashboard>> GetDashboardAsync(string username, int days = 7);
    }

    public interface INetworkService
    {
        Task<bool> CheckInternetConnectionAsync();
    }

    public interface IApiService
    {
        Task<ApiResult> RegisterAsync(string username, string password, string email, string code);
        Task<ApiResult> LoginAsync(string username, string password);
        Task LogoutAsync();
        void Logout();
        Task<CodeSendResult> SendCodeAsync(string email, string purpose);
        Task<ApiResult> VerifyCodeAsync(string email, string code, string purpose);
        Task<ApiResult<string>> FindIdAsync(string email, string code);
        Task<ApiResult> ResetPasswordAsync(string email, string code, string newPassword);
        Task<ApiResult> ChangePasswordAsync(string currentPassword, string newPassword);
        Task<ApiResult> DeleteAccountAsync();
        Task<UploadResult> UploadBookAsync(string filePath, string username, string requestId);
        Task<ApiResult<JobStatusResponse>> GetJobStatusAsync(string jobId);
        Task<ApiResult<JobStatusResponse>> CancelJobAsync(string jobId);
        Task<ApiResult> DownloadFileAsync(string url, string localPath);
        Task<ApiResult<byte[]>> DownloadBytesAsync(string url);
        Task<ApiResult<List<string>>> GetMusicFileListAsync(string username, string bookFolder);
        Task<ApiResult<List<ServerBook>>> GetMyServerBooksAsync(string username);
        Task<ApiResult> DeleteServerBookAsync(string bookFolder);
        Task<ApiResult<UsageBatchResponse>> SubmitUsageEventsAsync(List<UsageEvent> events);
        Task<ApiResult<UsageSummaryResponse>> GetUsageSummaryAsync();
        Task<ApiResult<UsageBookListResponse>> GetUsageBooksAsync();
        Task<ApiResult<UsageDailySeriesResponse>> GetUsageDailyAsync(int days);
        Task<ApiResult<BookProcessingHistoryResponse>> GetBookProcessingHistoryAsync(string bookFolder);
        Task<ApiResult<BookMusicTracksResponse>> GetBookMusicTracksAsync(string bookFolder);
    }
}