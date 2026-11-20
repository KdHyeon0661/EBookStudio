using System.Windows.Controls;
using System.IO;
using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.ViewModels;
using Xunit;

namespace EBookStudio.Tests;

public sealed class MyPageViewModelTests
{
    [Fact]
    public void SuccessfulPasswordChangeRequestsLogout()
    {
        StaTest.Run(() =>
        {
            var auth = new FakeAuthService();
            var dialogs = new FakeDialogService();
            var viewModel = CreateViewModel("password-user", auth, dialogs);
            bool logoutRequested = false;
            viewModel.RequestLogout += () => logoutRequested = true;
            var current = new PasswordBox { Password = "current-password" };
            var changed = new PasswordBox { Password = "new-password" };
            var confirmation = new PasswordBox { Password = "new-password" };

            ((AsyncRelayCommand)viewModel.ChangePasswordCommand)
                .ExecuteAsync(new object[] { current, changed, confirmation })
                .GetAwaiter().GetResult();

            Assert.True(logoutRequested);
            Assert.Equal(string.Empty, current.Password);
            Assert.Contains(dialogs.Messages, message => message.Contains("다시 로그인"));
        });
    }

    [Fact]
    public void ResetHistoryDeletesProgressFilesAndRefreshesLibrary()
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            string bookDirectory = Path.Combine(TestEnvironment.Root, "book-one");
            var files = new FakeBookFileSystem
            {
                DirectoryExistsResult = true,
                Directories = new[] { bookDirectory },
                FileExistsHandler = path => path.EndsWith("progress.json", StringComparison.Ordinal)
            };
            var viewModel = CreateViewModel("history-user", fileSystem: files);
            bool refreshRequested = false;
            viewModel.RequestReadingHistoryReset += () => refreshRequested = true;

            viewModel.ResetHistoryCommand.Execute(null);

            Assert.True(refreshRequested);
            Assert.Equal(Path.Combine(bookDirectory, "progress.json"), Assert.Single(files.DeletedFiles));
        });
    }

    [Fact]
    public void ResetUserDataRequestsLibraryReload()
    {
        StaTest.Run(() =>
        {
            var files = new FakeBookFileSystem();
            var viewModel = CreateViewModel("library-user", fileSystem: files);
            bool reloadRequested = false;
            viewModel.RequestLibraryReset += () =>
            {
                files.ResetLocalLibrary("library-user");
                reloadRequested = true;
                return Task.CompletedTask;
            };

            ((AsyncRelayCommand)viewModel.ResetUserDataCommand).ExecuteAsync(null).GetAwaiter().GetResult();

            Assert.True(reloadRequested);
            Assert.Equal("library-user", Assert.Single(files.ResetUsers));
        });
    }

    [Fact]
    public void FailedCoverDownloadLeavesNoDiscoverableBookJson()
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            string username = TestEnvironment.UniqueName("download-user");
            string folder = TestEnvironment.UniqueName("download-book");
            FileHelper.ResetUserData(username);
            int calls = 0;
            var auth = new FakeAuthService
            {
                DownloadFileHandler = async (_, path) =>
                {
                    calls++;
                    if (calls == 1)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        await File.WriteAllTextAsync(path, """{"chapters":[{"title":"Chapter"}]}""");
                        return ApiResult.Ok();
                    }
                    return ApiResult.Fail(new ApiError(ApiErrorKind.Network, "cover failed"));
                }
            };
            var viewModel = CreateViewModel(username, auth);
            var item = new ServerBookItem
            {
                Title = "Download Test",
                Folder = folder,
                TextFile = "download_full.json",
                CoverFile = "cover.png"
            };

            ((AsyncRelayCommand)viewModel.DownloadSingleServerBookCommand)
                .ExecuteAsync(item).GetAwaiter().GetResult();

            string finalDirectory = FileHelper.GetBookDirectory(username, folder);
            Assert.False(Directory.Exists(finalDirectory));
            string stagingDirectory = Path.Combine(FileHelper.BasePath, ".downloads");
            Assert.False(Directory.Exists(stagingDirectory)
                && Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories).Any());
        });
    }

    [Fact]
    public void BookWithoutCoverDownloadsAndIsImported()
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            string user = TestEnvironment.UniqueName("coverless-user");
            var item = new ServerBookItem { Title = "Coverless", Folder = "book", TextFile = "book_full.json" };
            var auth = new FakeAuthService
            {
                DownloadFileHandler = (_, path) =>
                {
                    File.WriteAllText(path, """{"chapters":[{"title":"Chapter"}]}""");
                    return Task.FromResult(ApiResult.Ok());
                }
            };
            var viewModel = CreateViewModel(user, auth);
            var imports = new List<ServerBookItem>();
            viewModel.RequestLibraryImport += (book, _) => imports.Add(book);

            ((AsyncRelayCommand)viewModel.DownloadSingleServerBookCommand).ExecuteAsync(item).GetAwaiter().GetResult();

            Assert.Same(item, Assert.Single(imports));
            Assert.True(File.Exists(FileHelper.GetLocalFilePath(user, item.Folder, "", item.TextFile)));
        });
    }

    [Fact]
    public void LibraryResetPreservesUsageAndRejectsAnInFlightDownload()
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            string user = TestEnvironment.UniqueName("reset-user");
            var item = new ServerBookItem { Title = "Transfer", Folder = "book", TextFile = "book_full.json" };
            var transfer = new TaskCompletionSource<ApiResult>();
            string? stagedPath = null;
            var auth = new FakeAuthService
            {
                DownloadFileHandler = (_, path) =>
                {
                    stagedPath = path;
                    return transfer.Task;
                }
            };
            var viewModel = CreateViewModel(user, auth);
            viewModel.RequestLibraryReset += () =>
            {
                FileHelper.ResetLocalLibrary(user);
                return Task.CompletedTask;
            };
            string usagePath = Path.Combine(FileHelper.GetUserDirectory(user), "usage_activity.json");
            Directory.CreateDirectory(Path.GetDirectoryName(usagePath)!);
            File.WriteAllText(usagePath, "saved-usage");
            string oldBook = FileHelper.GetBookDirectory(user, "old-book");
            Directory.CreateDirectory(oldBook);
            var imports = new List<ServerBookItem>();
            viewModel.RequestLibraryImport += (book, _) => imports.Add(book);

            Task download = ((AsyncRelayCommand)viewModel.DownloadSingleServerBookCommand).ExecuteAsync(item);
            Assert.NotNull(stagedPath);
            ((AsyncRelayCommand)viewModel.ResetUserDataCommand).ExecuteAsync(null).GetAwaiter().GetResult();
            File.WriteAllText(stagedPath, """{"chapters":[{"title":"Chapter"}]}""");
            transfer.SetResult(ApiResult.Ok());
            download.GetAwaiter().GetResult();

            Assert.Empty(imports);
            Assert.False(Directory.Exists(oldBook));
            Assert.False(File.Exists(FileHelper.GetLocalFilePath(user, item.Folder, "", item.TextFile)));
            Assert.Equal("saved-usage", File.ReadAllText(usagePath));
        });
    }

    [Fact]
    public void AtomicDownloadCommitRestoresExistingFilesWhenLaterCommitFails()
    {
        string root = Path.Combine(TestEnvironment.Root, TestEnvironment.UniqueName("atomic-commit"));
        string staging = Path.Combine(root, "staging");
        string destination = Path.Combine(root, "destination");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(destination);
        string stagedCover = Path.Combine(staging, "cover.new");
        string missingJson = Path.Combine(staging, "missing.json");
        string finalCover = Path.Combine(destination, "cover.png");
        File.WriteAllText(stagedCover, "new-cover");
        File.WriteAllText(finalCover, "old-cover");

        Assert.Throws<FileNotFoundException>(() =>
            MyPageViewModel.CommitStagedFilesAtomically(
                new[]
                {
                    (stagedCover, finalCover),
                    (missingJson, Path.Combine(destination, "book.json"))
                },
                Path.Combine(staging, ".rollback")));

        Assert.Equal("old-cover", File.ReadAllText(finalCover));
        Assert.False(File.Exists(Path.Combine(destination, "book.json")));
    }

    private static MyPageViewModel CreateViewModel(
        string username,
        FakeAuthService? auth = null,
        FakeDialogService? dialogs = null,
        FakeBookFileSystem? fileSystem = null)
        => new(
            username,
            auth ?? new FakeAuthService(),
            dialogs ?? new FakeDialogService(),
            fileSystem ?? new FakeBookFileSystem(),
            new FakeUsageService());
}
