using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.Services;
using EBookStudio.ViewModels;
using Xunit;

namespace EBookStudio.Tests;

public sealed class LibraryIsolationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CardActionWaitsForCancellationAndKeepsOnlyAlreadyReadableBooks(bool musicOnly)
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            string user = TestEnvironment.UniqueName("cancel-user");
            Book book = CreateBook("Cancelled book");
            book.IsAvailable = musicOnly;
            if (musicOnly)
            {
                book.MusicJobId = "pending-job";
                string path = FileHelper.GetLocalFilePath(user, book.FolderId, "", book.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, """{"chapters":[{"title":"Saved"}]}""");
            }
            else book.JobId = "pending-job";
            File.WriteAllText(FileHelper.GetLibraryFilePath(user), JsonSerializer.Serialize(new[] { book }));

            var poll = new TaskCompletionSource<ApiResult<JobStatusResponse>>();
            var libraryService = new MusicLibraryService(() => [], (_, _) => throw new NotSupportedException())
            {
                OnPoll = _ => poll.Task,
                OnCancel = id => Task.FromResult(ApiResult<JobStatusResponse>.Ok(
                    new JobStatusResponse { job_id = id, status = "cancel_requested" }))
            };
            var main = new MainViewModel(initializeClient: false)
            { LoggedInUser = user, IsNetworkAvailable = true, IsLoggedIn = true };
            var library = new LibraryViewModel(main, libraryService, new FakeDialogService());
            Task load = library.LoadLibrary();
            Assert.True(StaTest.PumpUntil(() => load.IsCompleted));
            load.GetAwaiter().GetResult();
            Book item = Assert.Single(library.DisplayBooks, current => !current.IsAddButton);
            Assert.Equal("작업 취소", item.CardActionText);

            Task cancel = ((AsyncRelayCommand)library.BookActionCommand).ExecuteAsync(item);
            Assert.True(StaTest.PumpUntil(() => cancel.IsCompleted));
            cancel.GetAwaiter().GetResult();
            Assert.True(item.HasPendingJob);
            Assert.Equal("작업 취소 처리 중", item.CardActionText);
            Assert.False(library.BookActionCommand.CanExecute(item));

            poll.SetResult(ApiResult<JobStatusResponse>.Ok(new JobStatusResponse { status = "cancelled" }));
            Assert.True(StaTest.PumpUntil(() => !item.HasPendingJob));
            if (musicOnly)
            {
                Assert.Contains(item, library.DisplayBooks);
                Assert.Equal("로컬 책 삭제", item.CardActionText);
                Assert.True(library.BookActionCommand.CanExecute(item));
                Assert.True(File.Exists(FileHelper.GetLocalFilePath(user, item.FolderId, "", item.FileName)));
            }
            else Assert.DoesNotContain(item, library.DisplayBooks);
        });
    }

    [Fact]
    public void AStaleLibraryLoadCannotReplaceTheNewAccountBooks()
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            string oldUser = TestEnvironment.UniqueName("old-user");
            string newUser = TestEnvironment.UniqueName("new-user");
            string oldPath = FileHelper.GetLibraryFilePath(oldUser);
            var oldRead = new TaskCompletionSource<string>();
            var fileSystem = new FakeBookFileSystem
            {
                FileExistsHandler = _ => true,
                ReadAllTextHandler = path => path == oldPath ? oldRead.Task :
                    Task.FromResult(JsonSerializer.Serialize(new[] { CreateBook("New book") }))
            };
            var main = new MainViewModel(initializeClient: false) { LoggedInUser = oldUser };
            var library = new LibraryViewModel(main, fileSystem: fileSystem);
            Task oldLoad = library.LoadLibrary();

            main.LoggedInUser = newUser;
            library.LoadLibrary().GetAwaiter().GetResult();
            oldRead.SetResult(JsonSerializer.Serialize(new[] { CreateBook("Old book") }));
            oldLoad.GetAwaiter().GetResult();

            Assert.Equal("New book", Assert.Single(library.DisplayBooks, book => !book.IsAddButton).Title);
        });
    }

    [Fact]
    public async Task MissingGeneratedAudioKeepsThePreviousBookUntilRetrySucceeds()
    {
        _ = TestEnvironment.Root;
        string cacheKey = Guid.NewGuid().ToString();
        Book book = CreateBook("Download test");
        string destination = FileHelper.GetLocalFilePath(cacheKey, book.FolderId, "", book.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string trackName = TestEnvironment.UniqueName("generated") + ".wav";
        const string existingContent = """{"chapters":[{"segments":[{"music_path":"music/default_ambient.wav"}]}]}""";
        string stagedContent = $$"""
            {"chapters":[{"segments":[{"music_path":"music/{{trackName}}"}]}]}
            """;
        File.WriteAllText(destination, existingContent);
        bool trackListed = true;
        bool wavAvailable = false;
        var service = new MusicLibraryService(() => trackListed ? [trackName] : [], (url, path) =>
        {
            if (url.EndsWith(book.FileName, StringComparison.Ordinal))
            {
                File.WriteAllText(path, stagedContent);
                return ApiResult.Ok();
            }
            if (!wavAvailable)
                return ApiResult.Fail(new ApiError(ApiErrorKind.Network, "Audio unavailable"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "audio bytes");
            return ApiResult.Ok();
        });
        var library = new LibraryViewModel(new MainViewModel(initializeClient: false), service);

        ApiResult failed = await library.DownloadMusicUpdateAsync("reader", cacheKey, book, CancellationToken.None);
        Assert.False(failed.Success);
        Assert.Equal(existingContent, File.ReadAllText(destination));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destination)!, ".music-update-*.tmp"));

        wavAvailable = true;
        trackListed = false;
        ApiResult incomplete = await library.DownloadMusicUpdateAsync("reader", cacheKey, book, CancellationToken.None);
        Assert.Equal(ApiErrorKind.InvalidResponse, incomplete.Error?.Kind);
        Assert.Equal(existingContent, File.ReadAllText(destination));

        trackListed = true;
        ApiResult succeeded = await library.DownloadMusicUpdateAsync("reader", cacheKey, book, CancellationToken.None);
        Assert.True(succeeded.Success);
        Assert.Equal(stagedContent, File.ReadAllText(destination));
        Assert.True(File.Exists(Path.Combine(FileHelper.MusicBasePath, trackName)));
    }

    private static Book CreateBook(string title) => new()
    {
        Title = title, FolderId = Guid.NewGuid().ToString(), FileName = "book_full.json", IsAvailable = true
    };

    private sealed class MusicLibraryService(Func<List<string>> tracks, Func<string, string, ApiResult> download)
        : ILibraryService
    {
        internal Func<string, Task<ApiResult<JobStatusResponse>>>? OnPoll { get; init; }
        internal Func<string, Task<ApiResult<JobStatusResponse>>>? OnCancel { get; init; }

        public Task<ApiResult> DownloadFileAsync(string url, string localPath, CancellationToken cancellationToken = default)
            => Task.FromResult(download(url, localPath));

        public Task<ApiResult<List<string>>> GetMusicFileListAsync(string username, string bookFolder)
            => Task.FromResult(ApiResult<List<string>>.Ok(tracks()));

        public Task<UploadResult> UploadBookAsync(string filePath, string username, string requestId)
            => throw new NotSupportedException();
        public Task<ApiResult<byte[]>> DownloadBytesAsync(string url)
            => throw new NotSupportedException();
        public Task<ApiResult<JobStatusResponse>> GetJobStatusAsync(string jobId)
            => OnPoll?.Invoke(jobId) ?? throw new NotSupportedException();
        public Task<ApiResult<JobStatusResponse>> CancelJobAsync(string jobId)
            => OnCancel?.Invoke(jobId) ?? throw new NotSupportedException();
    }
}
