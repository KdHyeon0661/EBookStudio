using System.Runtime.ExceptionServices;
using System.IO;
using System.Windows.Threading;
using EBookStudio.Models;
using EBookStudio.Services;

namespace EBookStudio.Tests;

internal static class TestEnvironment
{
    static TestEnvironment()
    {
        Root = Path.Combine(Path.GetTempPath(), "EBookStudio.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("EBOOK_LOCAL_DATA_ROOT", Root);
    }

    public static string Root { get; }

    public static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}

internal static class StaTest
{
    public static void Run(Action action)
    {
        ExceptionDispatchInfo? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { captured = ExceptionDispatchInfo.Capture(error); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        captured?.Throw();
    }

    internal static bool PumpUntil(Func<bool> done)
    {
        if (done()) return true;
        var frame = new DispatcherFrame();
        var expiry = DateTime.UtcNow.AddSeconds(5);
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => frame.Continue = !done() && DateTime.UtcNow < expiry;
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        return done();
    }
}

internal sealed class FakeDialogService : IDialogService
{
    public bool Confirmation { get; set; } = true;
    public List<string> Messages { get; } = new();

    public void ShowMessage(string message) => Messages.Add(message);
    public bool ShowConfirm(string message, string title) => Confirmation;
}

internal sealed class FakeBookFileSystem : IBookFileSystem
{
    public bool DirectoryExistsResult { get; set; }
    public string[] Directories { get; set; } = Array.Empty<string>();
    public Func<string, bool>? FileExistsHandler { get; set; }
    public Func<string, Task<string>>? ReadAllTextHandler { get; set; }
    public List<string> DeletedFiles { get; } = new();
    public List<string> ResetUsers { get; } = new();

    public bool FileExists(string path) => FileExistsHandler?.Invoke(path) ?? File.Exists(path);
    public Task<string> ReadAllTextAsync(string path) => ReadAllTextHandler?.Invoke(path) ?? File.ReadAllTextAsync(path);
    public bool DirectoryExists(string path) => DirectoryExistsResult || Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string[] GetDirectories(string path) => Directories;
    public void DeleteFile(string path) => DeletedFiles.Add(path);
    public void ResetUserData(string username) => ResetUsers.Add(username);
    public void ResetLocalLibrary(string cacheKey) => ResetUsers.Add(cacheKey);
}

internal sealed class FakeAuthService : IAuthService
{
    public ApiResult ChangePasswordResult { get; set; } = ApiResult.Ok();
    public CodeSendResult VerificationDelivery { get; set; } = new() { Success = true, Delivered = true };
    public Func<string, string, Task<ApiResult>>? DownloadFileHandler { get; set; }
    public ApiResult<List<string>> MusicFilesResult { get; set; }
        = ApiResult<List<string>>.Ok(new List<string>());

    public Func<string, string, Task<ApiResult<LocalAccount>>>? LoginHandler { get; set; }
    public Task<ApiResult<LocalAccount>> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        => LoginHandler?.Invoke(username, password) ??
            Task.FromResult(ApiResult<LocalAccount>.Ok(new LocalAccount(username, Guid.NewGuid())));
    public Task LogoutAsync() => Task.CompletedTask;
    public Task<CodeSendResult> SendVerificationCodeAsync(string email)
        => Task.FromResult(VerificationDelivery);
    public Task<ApiResult> VerifyCodeAsync(string email, string code)
        => Task.FromResult(ApiResult.Ok());
    public Task<ApiResult> RegisterAsync(string username, string password, string email, string code)
        => Task.FromResult(ApiResult.Ok());
    public Task<ApiResult<List<ServerBook>>> GetMyServerBooksAsync(string username)
        => Task.FromResult(ApiResult<List<ServerBook>>.Ok(new List<ServerBook>()));
    public Task<ApiResult> DeleteServerBookAsync(string bookFolder)
        => Task.FromResult(ApiResult.Ok());
    public Task<ApiResult> DownloadFileAsync(string url, string localPath, CancellationToken cancellationToken = default)
        => DownloadFileHandler?.Invoke(url, localPath) ?? Task.FromResult(ApiResult.Ok());

    public Task<ApiResult<byte[]>> DownloadBytesAsync(string url)
        => Task.FromResult(ApiResult<byte[]>.Fail(new ApiError(ApiErrorKind.NotFound, "No test image")));
    public Task<ApiResult<List<string>>> GetMusicFileListAsync(string username, string bookFolder)
        => Task.FromResult(MusicFilesResult);
    public Task<ApiResult> ChangePasswordAsync(string currentPassword, string newPassword)
        => Task.FromResult(ChangePasswordResult);
    public Task<ApiResult> DeleteAccountAsync() => Task.FromResult(ApiResult.Ok());
}

internal sealed class FakeUsageService : IUsageService
{
    private static readonly ApiError Unused = new(ApiErrorKind.Unknown, "unused in this test");

    public Task<ApiResult> SyncAsync(string username) => Task.FromResult(ApiResult.Ok());
    public Task<ApiResult<UsageSummaryResponse>> GetSummaryAsync(string username)
        => Task.FromResult(ApiResult<UsageSummaryResponse>.Fail(Unused));
    public Task<ApiResult<UsageDashboard>> GetDashboardAsync(string username, int days = 7)
        => Task.FromResult(ApiResult<UsageDashboard>.Fail(Unused));
}
