using EBookStudio.Helpers;
using EBookStudio.Models;
using static EBookStudio.Models.ApiService;

namespace EBookStudio.Tests
{
    public class FakeDialogService : IDialogService
    {
        public string LastMessage { get; private set; } = "";

        public bool ConfirmResult { get; set; } = true;

        public void ShowMessage(string message)
        {
            LastMessage = message;
        }

        public bool ShowConfirm(string message, string title)
        {
            LastMessage = message;
            return ConfirmResult;
        }
    }

    class FakeAccountService : IAccountService
    {
        public async Task<bool> SendCodeAsync(string email)
        {
            // 간단한 모의 로직: "fail"이 포함되면 실패 처리
            if (email.Contains("fail")) return false;
            return true;
        }

        public async Task<bool> VerifyCodeAsync(string email, string code)
        {
            return code == "123456"; // 코드가 123456이면 성공
        }

        public async Task<string?> FindIdAsync(string email)
        {
            return "TestUser";
        }

        public async Task<bool> ResetPasswordAsync(string e, string c, string p)
        {
            return true; // 무조건 성공 가정
        }
    }

    class FakeFilePickerService : IFilePickerService
    {
        public string? FilePathToReturn { get; set; }
        public string? PickPdfFile() => FilePathToReturn;
    }

    class FakeLibraryService : ILibraryService
    {
        public bool ShouldSucceed { get; set; } = true;

        // [수정됨] 튜플 대신 UploadResult 객체를 반환해야 함
        public async Task<UploadResult> UploadBookAsync(string f, string u)
        {
            if (ShouldSucceed)
            {
                return new UploadResult
                {
                    Success = true,
                    Text = "full.json",
                    Cover = "cover.png",
                    Author = "TestAuthor",
                    Message = "OK",
                    MusicFiles = new List<string> { "bgm.wav" }
                };
            }

            return new UploadResult
            {
                Success = false,
                Message = "Server Error"
            };
        }

        public async Task<byte[]> DownloadBytesAsync(string url) => new byte[10];
        public async Task<bool> DownloadFileAsync(string u, string p) => true;
        public async Task<List<string>> GetMusicFileListAsync(string u, string b) => new List<string>();
    }

    class FakeNoteService : INoteService
    {
        public bool IsRemoveCalled { get; private set; }
        public bool IsAddCalled { get; private set; }

        public (List<NoteItem>, List<NoteItem>, List<NoteItem>) FakeData { get; set; }

        public (List<NoteItem> Bookmarks, List<NoteItem> Highlights, List<NoteItem> Memos) LoadNotes(string u, string t)
            => FakeData;

        public void RemoveItem(string u, string t, NoteItem i)
        {
            IsRemoveCalled = true;
        }

        public void AddItem(string u, string t, NoteItem i)
        {
            IsAddCalled = true;
        }
    }

    class FakeSettingsService : ISettingsService
    {
        // 기존 속성
        public double FontSize { get; set; }
        public bool IsDarkModeCalled { get; private set; }

        // 인터페이스 확장에 따른 추가 구현 사항
        public System.Windows.Media.FontFamily FontFamily { get; set; } = new System.Windows.Media.FontFamily("Malgun Gothic");
        public double LineHeight { get; set; } = 40;
        public System.Windows.Media.Brush Background { get; set; } = System.Windows.Media.Brushes.White;
        public System.Windows.Media.Brush Foreground { get; set; } = System.Windows.Media.Brushes.Black;

        // 메서드 구현
        public void ApplyLightMode() { }
        public void ApplySepiaMode() { }
        public void ApplyDarkMode() => IsDarkModeCalled = true;
    }

    public class FakeAuthService : IAuthService
    {
        public bool LoginResult { get; set; }
        public bool SendCodeResult { get; set; }
        public bool VerifyCodeResult { get; set; }
        public bool RegisterResult { get; set; }
        public bool DeleteServerBookResult { get; set; }
        public bool DownloadResult { get; set; }

        // 서버에서 내려올 가짜 책 목록 저장소
        public List<ServerBookDto> MockServerBooks { get; set; } = new List<ServerBookDto>();
        public List<string> MockMusicFiles { get; set; } = new List<string>();

        public Task<bool> LoginAsync(string u, string p) => Task.FromResult(LoginResult);
        public Task<bool> SendVerificationCodeAsync(string e) => Task.FromResult(SendCodeResult);
        public Task<bool> VerifyCodeAsync(string e, string c) => Task.FromResult(VerifyCodeResult);
        public Task<bool> RegisterAsync(string u, string p, string e) => Task.FromResult(RegisterResult);

        // MyPage 관련 추가 메서드들 구현
        public Task<List<ServerBookDto>> GetMyServerBooksAsync(string username)
            => Task.FromResult(MockServerBooks);

        public Task<bool> DeleteServerBookAsync(string bookTitle)
            => Task.FromResult(DeleteServerBookResult);

        public Task<bool> DownloadFileAsync(string url, string localPath)
            => Task.FromResult(DownloadResult);

        public Task<List<string>> GetMusicFileListAsync(string username, string bookTitle)
            => Task.FromResult(MockMusicFiles);
    }

    class FakeBookFileSystem : IBookFileSystem
    {
        public bool IsDeleteFileCalled { get; private set; }
        public bool IsResetUserDataCalled { get; private set; }
        public string[] ExistingDirectories { get; set; } = new string[0];
        public List<string> ExistingFiles { get; set; } = new List<string>();

        public bool FileExists(string path) => ExistingFiles.Contains(path);
        public Task<string> ReadAllTextAsync(string path) => Task.FromResult("");
        public void DeleteFile(string path) => IsDeleteFileCalled = true;
        public bool DirectoryExists(string path) => true;
        public void CreateDirectory(string path) { }
        public string[] GetDirectories(string path) => ExistingDirectories;
        public void ResetUserData(string username) => IsResetUserDataCalled = true;
    }

    public class FakeNetworkService : INetworkService
    {
        public bool IsConnected { get; set; } = true;

        public Task<bool> CheckInternetConnectionAsync()
        {
            return Task.FromResult(IsConnected);
        }
    }
}
