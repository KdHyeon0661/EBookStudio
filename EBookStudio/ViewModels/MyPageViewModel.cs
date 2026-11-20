using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using EBookStudio.Helpers;
using EBookStudio.Services;
using EBookStudio.Models;

namespace EBookStudio.ViewModels
{

    public class MyPageViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IBookFileSystem _fileSystem;
        private readonly IUsageService _usageService;

        private string _username = string.Empty;
        private string _cacheKey = string.Empty;
        private long _accountVersion;
        private CancellationTokenSource _bookTransfers = new();
        private bool _isResettingLibrary;
        public string Username => _username;

        internal void SetAccount(string username, string cacheKey)
        {
            _accountVersion++;
            CancelBookTransfers();
            _username = username;
            _cacheKey = cacheKey;
            OnPropertyChanged(nameof(Username));
            ServerDeleteList.Clear();
            ServerDownloadList.Clear();
            TotalAppUsage = "0분";
            TotalReadingUsage = "0분";
            ReadingSessions = "0회";
            BooksRead = "0권";
            PageTurns = "0회";
            ActiveDays = "0일";
            BookUsageItems.Clear();
            DailyUsageItems.Clear();
            NotifyUsageCollections();
            UsageStatus = string.IsNullOrWhiteSpace(cacheKey) ? "로그인 후 확인할 수 있습니다." : "동기화 대기 중";
        }

        private string _totalAppUsage = "0분";
        public string TotalAppUsage { get => _totalAppUsage; private set { _totalAppUsage = value; OnPropertyChanged(); } }

        private string _totalReadingUsage = "0분";
        public string TotalReadingUsage { get => _totalReadingUsage; private set { _totalReadingUsage = value; OnPropertyChanged(); } }

        private string _readingSessions = "0회";
        public string ReadingSessions { get => _readingSessions; private set { _readingSessions = value; OnPropertyChanged(); } }

        private string _booksRead = "0권";
        public string BooksRead { get => _booksRead; private set { _booksRead = value; OnPropertyChanged(); } }

        private string _pageTurns = "0회";
        public string PageTurns { get => _pageTurns; private set { _pageTurns = value; OnPropertyChanged(); } }

        private string _activeDays = "0일";
        public string ActiveDays { get => _activeDays; private set { _activeDays = value; OnPropertyChanged(); } }

        private string _usageStatus = "동기화 대기 중";
        public string UsageStatus { get => _usageStatus; private set { _usageStatus = value; OnPropertyChanged(); } }

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    OnPropertyChanged();
                    if (_isDarkMode) SettingsService.Instance.ApplyDarkMode();
                    else SettingsService.Instance.ApplyLightMode();
                }
            }
        }

        public string AppVersion => "v1.0.0 (Build 2025)";
        public event Action? RequestLogout;
        public event Action<ServerBookItem, string>? RequestLibraryImport;
        public event Func<Task>? RequestLibraryReset;
        public event Action? RequestReadingHistoryReset;

        public ObservableCollection<ServerBookItem> ServerDeleteList { get; } = new ObservableCollection<ServerBookItem>();
        public ObservableCollection<ServerBookItem> ServerDownloadList { get; } = new ObservableCollection<ServerBookItem>();
        public ObservableCollection<BookUsageDisplayItem> BookUsageItems { get; } = new();
        public ObservableCollection<DailyUsageDisplayItem> DailyUsageItems { get; } = new();
        public bool HasBookUsage => BookUsageItems.Count > 0;
        public bool HasDailyUsage => DailyUsageItems.Count > 0;

        public ICommand ChangePasswordCommand { get; }
        public ICommand ResetHistoryCommand { get; }
        public ICommand ResetUserDataCommand { get; }
        public ICommand DeleteAccountCommand { get; }
        public ICommand LoadServerDataCommand { get; }
        public ICommand DeleteServerBooksCommand { get; }
        public ICommand DeleteSingleServerBookCommand { get; }
        public ICommand DownloadServerBooksCommand { get; }
        public ICommand DownloadSingleServerBookCommand { get; }
        public ICommand RefreshUsageCommand { get; }

        public MyPageViewModel(string username,
                               IAuthService? authService = null,
                               IDialogService? dialogService = null,
                               IBookFileSystem? fileSystem = null,
                               IUsageService? usageService = null)
        {
            SetAccount(username, username);
            _authService = authService ?? new AuthService();
            _dialogService = dialogService ?? new DialogService();
            _fileSystem = fileSystem ?? new BookFileSystem();
            _usageService = usageService ?? new UsageSyncService();

            ChangePasswordCommand = new AsyncRelayCommand(async o => await ExecuteChangePassword(o));
            RefreshUsageCommand = new AsyncRelayCommand(async o => await RefreshUsageAsync());
            ResetHistoryCommand = new RelayCommand(ExecuteResetHistory);
            ResetUserDataCommand = new AsyncRelayCommand(_ => ExecuteResetUserData());

            DeleteAccountCommand = new AsyncRelayCommand(async o => await ExecuteDeleteAccount());
            LoadServerDataCommand = new AsyncRelayCommand(async o => await LoadServerBooks());

            DeleteServerBooksCommand = new AsyncRelayCommand(async o =>
            {
                var selectedItems = ServerDeleteList.Where(x => x.IsSelected).ToList();
                if (selectedItems.Count > 0) await ExecuteDeleteServerBooks(selectedItems);
            });

            DeleteSingleServerBookCommand = new AsyncRelayCommand(async o =>
            {
                if (o is ServerBookItem item) await ExecuteDeleteServerBooks(new List<ServerBookItem> { item });
            });

            DownloadServerBooksCommand = new AsyncRelayCommand(async o =>
            {
                var selectedItems = ServerDownloadList.Where(x => x.IsSelected).ToList();
                if (selectedItems.Count > 0) await ExecuteDownloadServerBooks(selectedItems);
            });

            DownloadSingleServerBookCommand = new AsyncRelayCommand(async o =>
            {
                if (o is ServerBookItem item) await ExecuteDownloadServerBooks(new List<ServerBookItem> { item });
            });
        }

        public async Task RefreshUsageAsync()
        {
            string cacheKey = _cacheKey;
            long version = _accountVersion;
            if (string.IsNullOrWhiteSpace(cacheKey)) return;
            UsageStatus = "동기화 중...";
            ApiResult<UsageDashboard> result = await _usageService.GetDashboardAsync(cacheKey, 7);
            if (version != _accountVersion) return;
            if (!result.Success || result.Value == null)
            {
                UsageStatus = "오프라인 · 기록은 기기에 안전하게 대기 중";
                return;
            }

            UsageDashboard dashboard = result.Value;
            UsageSummaryResponse summary = dashboard.Summary;
            TotalAppUsage = FormatDuration(summary.TotalAppSeconds);
            TotalReadingUsage = FormatDuration(summary.TotalReadingSeconds);
            ReadingSessions = $"{summary.ReadingSessionCount:N0}회";
            BooksRead = $"{summary.BooksReadCount:N0}권";
            PageTurns = $"{summary.PageTurnCount:N0}회";
            ActiveDays = $"{summary.ActiveDayCount:N0}일";

            Dictionary<string, string> titles = LoadBookTitles(cacheKey);
            BookUsageItems.Clear();
            foreach (BookUsageSummaryResponse book in dashboard.Books)
            {
                string title = titles.GetValueOrDefault(book.BookId, book.BookId);
                BookUsageItems.Add(new BookUsageDisplayItem(
                    title, book.BookId, FormatDuration(book.TotalReadingSeconds),
                    $"세션 {book.ReadingSessionCount:N0}회 · 페이지 이동 {book.PageTurnCount:N0}회",
                    book.HighestProgressPercent, $"최고 진도 {book.HighestProgressPercent}%",
                    $"최근 독서 {FormatTimestamp(book.LastReadAt)}"));
            }

            DailyUsageItems.Clear();
            long maxDailySeconds = Math.Max(1, dashboard.Daily.Daily
                .Select(day => Math.Max(day.AppSeconds, day.ReadingSeconds)).DefaultIfEmpty(0).Max());
            foreach (DailyUsageResponse day in dashboard.Daily.Daily)
            {
                DailyUsageItems.Add(new DailyUsageDisplayItem(
                    FormatDate(day.Date), ScaleBar(day.AppSeconds, maxDailySeconds),
                    ScaleBar(day.ReadingSeconds, maxDailySeconds), FormatDuration(day.AppSeconds),
                    FormatDuration(day.ReadingSeconds),
                    FormatDuration(Math.Max(day.AppSeconds, day.ReadingSeconds))));
            }
            NotifyUsageCollections();
            UsageStatus = dashboard.IsCached
                ? "오프라인 · 마지막으로 동기화된 통계"
                : $"최근 7일 {FormatDuration(summary.Last7DaysAppSeconds)} · "
                    + $"활동 {summary.ActiveDayCount:N0}일";
        }

        private void NotifyUsageCollections()
        {
            OnPropertyChanged(nameof(HasBookUsage));
            OnPropertyChanged(nameof(HasDailyUsage));
        }

        private static Dictionary<string, string> LoadBookTitles(string username)
        {
            try
            {
                string path = FileHelper.GetLibraryFilePath(username);
                if (!File.Exists(path)) return new(StringComparer.Ordinal);
                List<Book>? books = JsonSerializer.Deserialize<List<Book>>(File.ReadAllText(path));
                return books?.Where(book => !string.IsNullOrWhiteSpace(book.FolderId))
                    .GroupBy(book => book.FolderId, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First().Title, StringComparer.Ordinal)
                    ?? new(StringComparer.Ordinal);
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[Usage Book Title Error] {error.Message}");
                return new(StringComparer.Ordinal);
            }
        }

        private static double ScaleBar(long seconds, long maximum)
            => seconds <= 0 ? 2 : Math.Max(6, seconds * 74d / maximum);

        private static string FormatDate(string value)
            => DateOnly.TryParse(value, out DateOnly date) ? date.ToString("MM.dd") : value;

        private static string FormatTimestamp(long epochSeconds)
            => DateTimeOffset.FromUnixTimeSeconds(epochSeconds).ToLocalTime().ToString("yyyy.MM.dd HH:mm");

        private static string FormatDuration(long seconds)
        {
            if (seconds < 60) return $"{seconds}초";
            long hours = seconds / 3600;
            long minutes = seconds % 3600 / 60;
            return hours > 0 ? $"{hours}시간 {minutes}분" : $"{minutes}분";
        }

        private async Task ExecuteChangePassword(object? parameter)
        {
            var boxes = parameter as object[];
            if (boxes == null || boxes.Length < 3) return;
            var currentBox = boxes[0] as PasswordBox;
            var newBox = boxes[1] as PasswordBox;
            var confirmBox = boxes[2] as PasswordBox;
            if (currentBox == null || newBox == null || confirmBox == null) return;
            if (string.IsNullOrWhiteSpace(currentBox.Password) || string.IsNullOrWhiteSpace(newBox.Password))
            { _dialogService.ShowMessage("현재 비밀번호와 새 비밀번호를 입력해주세요."); return; }
            if (newBox.Password.Length < 8)
            { _dialogService.ShowMessage("새 비밀번호는 8자 이상이어야 합니다."); return; }
            if (newBox.Password != confirmBox.Password)
            { _dialogService.ShowMessage("비밀번호 확인이 일치하지 않습니다."); return; }

            long version = _accountVersion;
            ApiResult result = await _authService.ChangePasswordAsync(currentBox.Password, newBox.Password);
            if (version != _accountVersion) return;
            if (!result.Success)
            { _dialogService.ShowMessage($"비밀번호 변경 실패: {result.Error?.UserMessage ?? "요청을 처리하지 못했습니다."}"); return; }
            currentBox.Password = ""; newBox.Password = ""; confirmBox.Password = "";
            _dialogService.ShowMessage("비밀번호가 변경되었습니다. 새 비밀번호로 다시 로그인해주세요.");
            RequestLogout?.Invoke();
        }

        private void ExecuteResetHistory(object? obj)
        {
            if (_dialogService.ShowConfirm("모든 책의 읽은 기록(진도율)을 초기화하시겠습니까?", "확인"))
            {
                try
                {
                    string userDir = FileHelper.GetUserDirectory(_cacheKey);
                    if (_fileSystem.DirectoryExists(userDir))
                    {
                        var bookDirs = _fileSystem.GetDirectories(userDir);
                        foreach (var dir in bookDirs)
                        {
                            string progressPath = Path.Combine(dir, "progress.json");
                            if (_fileSystem.FileExists(progressPath)) _fileSystem.DeleteFile(progressPath);
                        }
                    }
                    RequestReadingHistoryReset?.Invoke();
                    _dialogService.ShowMessage("진도율이 초기화되었습니다.");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"초기화 실패: {ex.Message}");
                }
            }
        }

        private async Task ExecuteResetUserData()
        {
            string cacheKey = _cacheKey;
            long version = _accountVersion;
            if (string.IsNullOrWhiteSpace(cacheKey) ||
                !_dialogService.ShowConfirm("이 PC의 보관함을 비우시겠습니까?\n책과 해당 책의 노트가 삭제됩니다.", "경고") ||
                version != _accountVersion) return;
            _isResettingLibrary = true;
            CancelBookTransfers();
            try
            {
                if (RequestLibraryReset is { } reset) await reset();
                else _fileSystem.ResetLocalLibrary(cacheKey);
                if (version == _accountVersion) _dialogService.ShowMessage("보관함이 비워졌습니다.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { _dialogService.ShowMessage($"보관함을 비우지 못했습니다. {error.Message}"); }
            finally { _isResettingLibrary = false; }
        }

        private void CancelBookTransfers()
        {
            var previous = _bookTransfers;
            _bookTransfers = new CancellationTokenSource();
            previous.Cancel();
            previous.Dispose();
        }

        private async Task ExecuteDeleteAccount()
        {
            if (!_dialogService.ShowConfirm("정말로 탈퇴하시겠습니까?\n서버 계정과 모든 도서가 삭제되며 복구할 수 없습니다.", "탈퇴"))
                return;
            string deletedCacheKey = _cacheKey;
            long version = _accountVersion;
            ApiResult result = await _authService.DeleteAccountAsync();
            if (version != _accountVersion) return;
            if (!result.Success)
            {
                _dialogService.ShowMessage($"계정 삭제 실패: {result.Error?.UserMessage ?? "요청을 처리하지 못했습니다."}");
                return;
            }
            RequestLogout?.Invoke();
            try
            {
                _fileSystem.ResetUserData(deletedCacheKey);
                _dialogService.ShowMessage("계정과 서버 데이터가 삭제되었습니다.");
            }
            catch (IOException error)
            {
                _dialogService.ShowMessage($"서버 계정은 삭제됐지만 로컬 파일 정리에 실패했습니다.\n{error.Message}");
            }
        }

        private async Task LoadServerBooks()
        {
            long version = _accountVersion;
            string username = Username;
            ApiResult<List<ServerBook>> result = await _authService.GetMyServerBooksAsync(username);
            if (version != _accountVersion) return;
            if (!result.Success)
            {
                _dialogService.ShowMessage($"서버 보관함 조회 실패: {result.Error?.UserMessage ?? "요청을 처리하지 못했습니다."}");
                return;
            }
            ServerDeleteList.Clear();
            ServerDownloadList.Clear();

            var coverTargets = new List<(ServerBookItem, ServerBookItem)>();
            foreach (var b in result.Value ?? new List<ServerBook>())
            {
                // [수정] Folder(UUID) 정보도 같이 저장해야 함
                var item = new ServerBookItem
                {
                    Title = b.title,
                    Folder = b.folder,
                    CoverUrl = b.cover_url,
                    CoverFile = b.cover_file,
                    TextFile = b.text_file,
                    Author = b.author
                };

                ServerDeleteList.Add(item);

                // 다운로드 리스트에는 객체를 새로 만들어야 UI 상태(IsSelected 등)가 꼬이지 않음
                var downloadItem = new ServerBookItem
                {
                    Title = b.title,
                    Folder = b.folder,
                    CoverUrl = b.cover_url,
                    CoverFile = b.cover_file,
                    TextFile = b.text_file,
                    Author = b.author
                };
                ServerDownloadList.Add(downloadItem);
                if (!string.IsNullOrWhiteSpace(b.cover_file)) coverTargets.Add((item, downloadItem));
            }
            _ = LoadServerCoversAsync(coverTargets, username, version);
        }

        private async Task LoadServerCoversAsync(List<(ServerBookItem DeleteItem, ServerBookItem DownloadItem)> items,
                                                 string username, long version)
        {
            foreach (var (deleteItem, downloadItem) in items)
            {
                if (version != _accountVersion || !ServerDownloadList.Contains(downloadItem)) return;
                try
                {
                    var result = await _authService.DownloadBytesAsync(FileUrl(username, downloadItem.Folder, downloadItem.CoverFile));
                    if (version != _accountVersion) return;
                    if (!result.Success || result.Value is not { Length: > 0 } bytes) continue;
                    using var imageBytes = new MemoryStream(bytes, writable: false);
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelHeight = 100;
                    image.StreamSource = imageBytes;
                    image.EndInit();
                    image.Freeze();
                    deleteItem.CoverImage = downloadItem.CoverImage = image;
                }
                catch (Exception error) when (error is IOException or ArgumentException or
                                             NotSupportedException or InvalidOperationException or FormatException)
                { System.Diagnostics.Debug.WriteLine($"[Book cover] {error.Message}"); }
            }
        }

        private async Task ExecuteDeleteServerBooks(List<ServerBookItem> items)
        {
            if (items.Count == 0) return;
            if (!_dialogService.ShowConfirm($"{items.Count}개의 책을 서버에서 삭제하시겠습니까?\n(음악 파일은 보존됩니다)", "서버 삭제")) return;

            long version = _accountVersion;
            int succeeded = 0;
            ApiError? lastError = null;
            foreach (var item in items)
            {
                ApiResult result = await _authService.DeleteServerBookAsync(item.Folder);
                if (version != _accountVersion) return;
                if (result.Success)
                {
                    succeeded++;
                    ServerDeleteList.Remove(item);
                    var dlItem = ServerDownloadList.FirstOrDefault(x => x.Folder == item.Folder);
                    if (dlItem != null) ServerDownloadList.Remove(dlItem);
                }
                else
                {
                    lastError = result.Error;
                }
            }
            string message = $"{succeeded}/{items.Count}개 도서를 서버에서 삭제했습니다.";
            if (lastError != null) message += $"\n실패 원인: {lastError.UserMessage}";
            _dialogService.ShowMessage(message);
        }

        private async Task ExecuteDownloadServerBooks(List<ServerBookItem> items)
        {
            if (items.Count == 0 || _isResettingLibrary) return;
            string username = Username;
            string cacheKey = _cacheKey;
            long version = _accountVersion;
            CancellationToken cancellationToken = _bookTransfers.Token;
            int succeeded = 0;
            foreach (var item in items)
            {
                bool downloaded = await ProcessDownloadBook(item, username, cacheKey, cancellationToken);
                if (version != _accountVersion || cancellationToken.IsCancellationRequested) return;
                if (!downloaded) continue;
                succeeded++;
                RequestLibraryImport?.Invoke(item, cacheKey);
            }
            _dialogService.ShowMessage($"{succeeded}/{items.Count}개 도서를 다운로드했습니다.");
        }

        private async Task<bool> ProcessDownloadBook(ServerBookItem item, string username, string cacheKey,
                                                      CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || string.IsNullOrWhiteSpace(cacheKey)) return false;
            if (string.IsNullOrWhiteSpace(item.TextFile))
            {
                _dialogService.ShowMessage($"'{item.Title}'의 분석 결과가 아직 준비되지 않았습니다.");
                return false;
            }

            string stagingRoot = Path.Combine(FileHelper.BasePath, ".downloads", Guid.NewGuid().ToString("N"));
            string stagingMusic = Path.Combine(stagingRoot, "music");
            string rollbackDirectory = Path.Combine(stagingRoot, ".rollback");
            string bookDirectory = FileHelper.GetBookDirectory(cacheKey, item.Folder);
            bool bookDirectoryExisted = Directory.Exists(bookDirectory);
            bool committed = false;
            try
            {
                Directory.CreateDirectory(stagingRoot);
                string stagedJson = Path.Combine(stagingRoot, Path.GetFileName(item.TextFile));
                ApiResult jsonResult = await _authService.DownloadFileAsync(
                    FileUrl(username, item.Folder, item.TextFile), stagedJson, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!jsonResult.Success)
                {
                    _dialogService.ShowMessage($"'{item.Title}' 본문 다운로드 실패: {jsonResult.Error?.UserMessage}");
                    return false;
                }

                var chapters = BookContent.ParseChapters(await File.ReadAllTextAsync(stagedJson, cancellationToken));
                var commits = new List<(string StagedPath, string DestinationPath)>();
                if (!string.IsNullOrWhiteSpace(item.CoverFile))
                {
                    string stagedCover = Path.Combine(stagingRoot, Path.GetFileName(item.CoverFile));
                    ApiResult coverResult = await _authService.DownloadFileAsync(
                        FileUrl(username, item.Folder, item.CoverFile), stagedCover, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!coverResult.Success)
                    {
                        _dialogService.ShowMessage($"'{item.Title}' 표지 다운로드 실패: {coverResult.Error?.UserMessage}");
                        return false;
                    }
                    commits.Add((stagedCover, FileHelper.GetLocalFilePath(cacheKey, item.Folder, "", item.CoverFile)));
                }

                if (!await DownloadMusicFromList(username, item.Folder, stagingMusic, cancellationToken)) return false;
                cancellationToken.ThrowIfCancellationRequested();
                bool HasMusic(string path) => File.Exists(path) ||
                    (Path.GetDirectoryName(path) == FileHelper.MusicBasePath &&
                     File.Exists(Path.Combine(stagingMusic, Path.GetFileName(path))));
                if (BookContent.MusicReferences(chapters).Any(reference =>
                    BookContent.FindLocalMusic(cacheKey, item.Folder, reference, HasMusic) == null))
                {
                    _dialogService.ShowMessage($"'{item.Title}' 본문과 음악 파일이 일치하지 않습니다. 다시 다운로드해주세요.");
                    return false;
                }
                if (Directory.Exists(stagingMusic))
                {
                    foreach (string stagedMusicFile in Directory.EnumerateFiles(stagingMusic))
                    {
                        commits.Add((stagedMusicFile,
                            Path.Combine(FileHelper.MusicBasePath, Path.GetFileName(stagedMusicFile))));
                    }
                }
                commits.Add((stagedJson, FileHelper.GetLocalFilePath(
                    cacheKey, item.Folder, "", item.TextFile)));

                cancellationToken.ThrowIfCancellationRequested();
                CommitStagedFilesAtomically(commits, rollbackDirectory);
                committed = true;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
            catch (JsonException error)
            {
                _dialogService.ShowMessage($"'{item.Title}' 본문 형식 오류: {error.Message}");
                return false;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _dialogService.ShowMessage($"'{item.Title}' 다운로드 파일 반영 실패: {error.Message}");
                return false;
            }
            finally
            {
                try
                {
                    bool recoveryFilesRemain = !committed && Directory.Exists(rollbackDirectory)
                        && Directory.EnumerateFiles(rollbackDirectory).Any();
                    if (!recoveryFilesRemain && Directory.Exists(stagingRoot))
                        Directory.Delete(stagingRoot, recursive: true);
                    else if (recoveryFilesRemain)
                        System.Diagnostics.Debug.WriteLine(
                            $"[Download recovery files preserved] {rollbackDirectory}");
                }
                catch (Exception cleanupError)
                {
                    System.Diagnostics.Debug.WriteLine($"[Download staging cleanup] {cleanupError}");
                }

                if (!committed && !bookDirectoryExisted)
                {
                    try
                    {
                        if (Directory.Exists(bookDirectory) &&
                            !Directory.EnumerateFileSystemEntries(bookDirectory).Any())
                            Directory.Delete(bookDirectory);
                    }
                    catch (Exception cleanupError)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Download book cleanup] {cleanupError}");
                    }
                }
            }
        }

        internal static void CommitStagedFilesAtomically(
            IReadOnlyList<(string StagedPath, string DestinationPath)> commits,
            string rollbackDirectory)
        {
            var applied = new List<(string DestinationPath, string? BackupPath)>();
            Directory.CreateDirectory(rollbackDirectory);

            try
            {
                for (int index = 0; index < commits.Count; index++)
                {
                    (string stagedPath, string destinationPath) = commits[index];
                    string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                    if (string.IsNullOrWhiteSpace(destinationDirectory))
                        throw new IOException("다운로드 대상 폴더가 올바르지 않습니다.");
                    if (!File.Exists(stagedPath))
                        throw new FileNotFoundException("준비된 다운로드 파일을 찾을 수 없습니다.", stagedPath);

                    Directory.CreateDirectory(destinationDirectory);
                    string? backupPath = null;
                    if (File.Exists(destinationPath))
                    {
                        backupPath = Path.Combine(rollbackDirectory, $"{index:D4}.bak");
                        File.Move(destinationPath, backupPath);
                    }
                    // Record the backup before installing the new file. If that move fails,
                    // the common rollback path must still restore this current destination.
                    applied.Add((destinationPath, backupPath));
                    File.Move(stagedPath, destinationPath);
                }
            }
            catch (Exception commitError)
            {
                Exception? rollbackError = null;
                for (int index = applied.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        var entry = applied[index];
                        if (File.Exists(entry.DestinationPath)) File.Delete(entry.DestinationPath);
                        if (entry.BackupPath != null && File.Exists(entry.BackupPath))
                            File.Move(entry.BackupPath, entry.DestinationPath);
                    }
                    catch (Exception error)
                    {
                        rollbackError ??= error;
                    }
                }

                if (rollbackError != null)
                    throw new IOException("다운로드 반영과 기존 파일 복구가 모두 실패했습니다.",
                        new AggregateException(commitError, rollbackError));
                throw;
            }
        }
        private static string FileUrl(string username, string bookFolder, params string[] segments)
        {
            var encoded = new[] { username, bookFolder }.Concat(segments)
                .Select(Uri.EscapeDataString);
            return $"{ApiConfig.BaseUrl}/files/{string.Join("/", encoded)}";
        }

        private async Task<bool> DownloadMusicFromList(string username, string bookFolder,
                                                       string stagingMusicFolder, CancellationToken cancellationToken)
        {
            ApiResult<List<string>> listResult = await _authService.GetMusicFileListAsync(username, bookFolder);
            cancellationToken.ThrowIfCancellationRequested();
            if (!listResult.Success)
            {
                _dialogService.ShowMessage($"음악 목록 조회 실패: {listResult.Error?.UserMessage}");
                return false;
            }
            List<string> musicFiles = listResult.Value ?? new List<string>();
            if (musicFiles.Count == 0) return true;

            Directory.CreateDirectory(stagingMusicFolder);

            foreach (var file in musicFiles)
            {
                string finalPath = FileHelper.GetLocalFilePath(username, bookFolder, "music", file);
                if (!_fileSystem.FileExists(finalPath))
                {
                    string localPath = Path.Combine(stagingMusicFolder, Path.GetFileName(finalPath));
                    string serverUrl = FileUrl(username, bookFolder, "music", file);
                    ApiResult downloadResult = await _authService.DownloadFileAsync(serverUrl, localPath, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!downloadResult.Success)
                    {
                        _dialogService.ShowMessage($"음악 다운로드 실패: {downloadResult.Error?.UserMessage}");
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
