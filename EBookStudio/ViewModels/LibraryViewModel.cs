using EBookStudio.Helpers;
using EBookStudio.Services;
using EBookStudio.Models;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace EBookStudio.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        private enum JobKind { Analysis, Music }

        private readonly MainViewModel _mainVM;
        private readonly ILibraryService _libraryService;
        private readonly IBookFileSystem _fileSystem;
        private long _loadVersion;
        private bool _isLoadingLibrary;

        private readonly record struct LibraryContext(string Username, string CacheKey, long Version);
        private LibraryContext CurrentContext => new(_mainVM.LoggedInUser, _mainVM.LocalCacheKey, _loadVersion);
        private bool IsCurrent(LibraryContext context) => CurrentContext == context;
        private readonly IDialogService _dialogService;
        private readonly IFilePickerService _filePickerService;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobMonitors = new();

        private readonly List<Book> _allBooks = new();
        public ObservableCollection<Book> DisplayBooks { get; } = new();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); RefreshList(); }
        }

        private string _selectedSortOption = "최신생성순";
        public List<string> SortOptions { get; } = new()
        {
            "최신생성순", "오래된순", "이름순", "이름역순", "작가이름순", "작가이름 역순"
        };

        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set { _selectedSortOption = value; OnPropertyChanged(); RefreshList(); }
        }

        public ICommand AddBookCommand { get; }
        public ICommand OpenBookCommand { get; }
        public ICommand BookActionCommand { get; }

        public LibraryViewModel(MainViewModel mainVM,
                                ILibraryService? libraryService = null,
                                IDialogService? dialogService = null,
                                IFilePickerService? filePickerService = null,
                                IBookFileSystem? fileSystem = null)
        {
            _mainVM = mainVM;
            _libraryService = libraryService ?? new LibraryService();
            _fileSystem = fileSystem ?? new BookFileSystem();
            _dialogService = dialogService ?? new DialogService();
            _filePickerService = filePickerService ?? new FilePickerService();

            AddBookCommand = new AsyncRelayCommand(
                async _ => await UploadProcess(), _ => !_isLoadingLibrary && _mainVM.IsNetworkAvailable);
            OpenBookCommand = new RelayCommand(parameter =>
            {
                if (!_isLoadingLibrary && parameter is Book book && _allBooks.Contains(book) && book.IsAvailable)
                    _mainVM.NavigateToBookDetail(book);
            });
            BookActionCommand = new AsyncRelayCommand(async parameter =>
            {
                if (parameter is not Book book) return;
                if (book.HasCancelableJob) await CancelProcessing(book);
                else await DeleteBook(book);
            }, parameter => !_isLoadingLibrary && parameter is Book book && _allBooks.Contains(book) &&
                           (!book.HasCancelableJob || !book.IsCancellationRequested));
            RefreshList();
        }

        public void StopMonitoring()
        {
            foreach (var entry in _jobMonitors.ToArray())
            {
                if (_jobMonitors.TryRemove(entry.Key, out CancellationTokenSource? source))
                    source.Cancel();
            }
        }

        internal void UpdateJobControls()
        {
            bool online = _mainVM.IsLoggedIn && _mainVM.IsNetworkAvailable;
            if (!online) StopMonitoring();
            LibraryContext context = CurrentContext;
            foreach (Book book in _allBooks.ToList())
            {
                book.JobControlsAvailable = online;
                if (online)
                {
                    StartJobMonitor(book, book.JobId, JobKind.Analysis, context);
                    StartJobMonitor(book, book.MusicJobId, JobKind.Music, context);
                }
                else if (book.HasPendingJob && book.IsAvailable)
                {
                    book.StatusMessage = "음악 상태 확인 대기 · 온라인에서 갱신";
                }
            }
            CommandManager.InvalidateRequerySuggested();
        }

        internal async Task ResetLocalLibraryAsync()
        {
            _loadVersion++;
            LibraryContext context = CurrentContext;
            _isLoadingLibrary = true;
            StopMonitoring();
            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _saveLock.WaitAsync();
                try
                {
                    if (IsCurrent(context)) _fileSystem.ResetLocalLibrary(context.CacheKey);
                }
                finally { _saveLock.Release(); }
            }
            finally
            {
                if (IsCurrent(context)) await LoadLibrary();
            }
        }

        private async Task DeleteBook(Book book)
        {
            LibraryContext context = CurrentContext;
            if (!_dialogService.ShowConfirm($"이 PC에서 '{book.Title}' 책을 삭제하시겠습니까?", "삭제 확인"))
                return;
            if (!IsCurrent(context) || !_allBooks.Contains(book) || book.HasCancelableJob) return;
            if (!DeleteLocalBookFiles(book, context.CacheKey)) return;
            StopMonitor(book.JobId);
            StopMonitor(book.MusicJobId);
            RemoveBookFromLibrary(book);
            await SaveLibrary();
        }

        private async Task CancelProcessing(Book book)
        {
            if (!book.HasCancelableJob || book.IsCancellationRequested) return;
            if (!book.JobControlsAvailable)
            {
                _dialogService.ShowMessage("서버에 연결하고 로그인한 뒤 작업을 취소할 수 있습니다.");
                return;
            }
            LibraryContext context = CurrentContext;
            JobKind kind = string.IsNullOrWhiteSpace(book.JobId) ? JobKind.Music : JobKind.Analysis;
            string jobId = kind == JobKind.Analysis ? book.JobId : book.MusicJobId;
            if (!_dialogService.ShowConfirm($"'{book.Title}'의 백그라운드 작업을 취소하시겠습니까?", "작업 취소"))
                return;
            if (!StillOwnsJob()) return;
            ApiResult<JobStatusResponse> result = await _libraryService.CancelJobAsync(jobId);
            if (!StillOwnsJob()) return;
            if (!result.Success)
            {
                if (result.Error?.Kind == ApiErrorKind.NotFound)
                {
                    StopMonitor(jobId);
                    await HandleCancelledJob(book, kind, context.CacheKey);
                }
                else _dialogService.ShowMessage($"작업 취소 실패: {result.Error?.UserMessage}");
                return;
            }
            switch (result.Value?.status)
            {
                case "cancelled":
                    StopMonitor(jobId);
                    await HandleCancelledJob(book, kind, context.CacheKey);
                    break;
                case "cancel_requested":
                    await MarkCancellationRequested(book);
                    if (StillOwnsJob()) StartJobMonitor(book, jobId, kind, context);
                    break;
                case "done" or "error" or "skipped":
                    // The worker finished before the cancellation arrived. Apply its actual result.
                    if (kind == JobKind.Music && result.Value.status == "done")
                        await MarkMusicDownloadPending(book);
                    if (StillOwnsJob()) StartJobMonitor(book, jobId, kind, context);
                    break;
                default:
                    _dialogService.ShowMessage("서버에서 작업 취소를 확인하지 못했습니다. 잠시 후 다시 시도해주세요.");
                    break;
            }

            bool StillOwnsJob() => IsCurrent(context) && _allBooks.Contains(book) &&
                (kind == JobKind.Analysis ? book.JobId : book.MusicJobId) == jobId;
        }

        private void RemoveBookFromLibrary(Book book)
        {
            _allBooks.Remove(book);
            RefreshList();
        }

        private bool DeleteLocalBookFiles(Book book, string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(book.FolderId)) return true;
            string directory = FileHelper.GetBookDirectory(cacheKey, book.FolderId);
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _dialogService.ShowMessage($"로컬 책 파일 삭제 실패: {error.Message}");
                return false;
            }
        }

        private async Task SaveLibrary()
        {
            LibraryContext context = CurrentContext;
            await _saveLock.WaitAsync();
            try
            {
                if (!IsCurrent(context) || string.IsNullOrWhiteSpace(context.CacheKey)) return;
                string path = FileHelper.GetLibraryFilePath(context.CacheKey);
                string json = JsonSerializer.Serialize(_allBooks.Where(book => !book.IsAddButton).ToList(),
                    new JsonSerializerOptions { WriteIndented = true });
                await AtomicFile.WriteAllTextAsync(path, json);
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"저장 실패: {error}");
                _dialogService.ShowMessage("보관함 정보를 안전하게 저장하지 못했습니다. 저장 공간과 권한을 확인해주세요.");
            }
            finally { _saveLock.Release(); }
        }

        public async Task LoadLibrary()
        {
            _loadVersion++;
            StopMonitoring();
            LibraryContext context = CurrentContext;
            _allBooks.Clear();
            _isLoadingLibrary = !string.IsNullOrWhiteSpace(context.CacheKey);
            RefreshList();
            CommandManager.InvalidateRequerySuggested();
            if (!_isLoadingLibrary) return;

            try
            {
                var loaded = new List<Book>();
                try
                {
                    string path = FileHelper.GetLibraryFilePath(context.CacheKey);
                    if (_fileSystem.FileExists(path))
                    {
                        string json = await _fileSystem.ReadAllTextAsync(path);
                        if (!IsCurrent(context)) return;
                        loaded = JsonSerializer.Deserialize<List<Book>>(json) ?? new();
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
                {
                    System.Diagnostics.Debug.WriteLine($"[Library load] {error}");
                }

                await Task.Run(() => LoadLocalBooks(context.CacheKey, loaded));
                if (!IsCurrent(context)) return;

                foreach (Book book in loaded)
                {
                    if (!_allBooks.Any(existing => existing.FolderId == book.FolderId &&
                                             (!string.IsNullOrEmpty(book.FolderId) || existing.JobId == book.JobId)))
                        _allBooks.Add(book);
                }
                RefreshList();
                UpdateJobControls();
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[Library scan] {error}");
                if (IsCurrent(context))
                    _dialogService.ShowMessage("로컬 보관함을 읽지 못했습니다. 파일 접근 권한을 확인해주세요.");
            }
            finally
            {
                if (IsCurrent(context))
                {
                    _isLoadingLibrary = false;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public async Task ImportDownloadedBook(ServerBookItem item, string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey) || cacheKey != _mainVM.LocalCacheKey ||
                string.IsNullOrWhiteSpace(item.Folder)) return;
            string localText = FileHelper.GetLocalFilePath(cacheKey, item.Folder, string.Empty, item.TextFile);
            if (!File.Exists(localText)) return;

            Book? book = _allBooks.FirstOrDefault(existing => existing.FolderId == item.Folder);
            if (book == null)
            {
                book = new Book { FolderId = item.Folder, CreatedAt = DateTime.Now };
                _allBooks.Insert(0, book);
            }
            book.Title = item.Title;
            book.Author = item.Author;
            book.FileName = item.TextFile;
            string localCover = FileHelper.GetLocalFilePath(cacheKey, item.Folder, string.Empty, item.CoverFile);
            book.CoverUrl = File.Exists(localCover) ? localCover : string.Empty;
            book.IsBusy = false;
            book.IsAvailable = true;
            book.StatusMessage = "서버 보관함에서 다운로드 완료";
            await SaveLibrary();
            RefreshList();
        }

        public async Task ResetReadingProgressAsync()
        {
            ResetReadingProgress(_allBooks);
            await SaveLibrary();
            RefreshList();
        }

        internal static void ResetReadingProgress(IEnumerable<Book> books)
        {
            foreach (Book book in books.Where(book => !book.IsAddButton))
                book.LastPage = 0;
        }

        private static void LoadLocalBooks(string cacheKey, List<Book> books)
        {
            foreach (Book book in books)
            {
                if (!string.IsNullOrWhiteSpace(book.JobId) && !book.IsAvailable)
                {
                    book.IsBusy = true;
                    book.IsAvailable = false;
                    book.StatusMessage = "분석 작업 복구 대기...";
                }
                else if (book.IsBusy)
                {
                    book.IsBusy = false;
                    book.IsAvailable = false;
                    book.StatusMessage = "이전 버전의 중단된 업로드";
                }

                if (string.IsNullOrWhiteSpace(book.FolderId)) continue;
                ApplyLocalProgress(book, cacheKey);
                if (book.IsAvailable)
                {
                    string text = FileHelper.GetLocalFilePath(cacheKey, book.FolderId, "", book.FileName);
                    if (!File.Exists(text))
                    {
                        string root = FileHelper.GetBookDirectory(cacheKey, book.FolderId);
                        string? discovered = Directory.Exists(root) ? Directory.EnumerateFiles(root, "*_full.json").FirstOrDefault() : null;
                        if (discovered != null) book.FileName = Path.GetFileName(discovered);
                        else
                        {
                            book.IsAvailable = false;
                            book.StatusMessage = "본문 파일이 없습니다 · 서버 보관함에서 다시 받아주세요.";
                        }
                    }
                }
                string? coverFile = Path.GetFileName(book.CoverUrl);
                if (string.IsNullOrEmpty(coverFile)) continue;
                string cover = FileHelper.GetLocalFilePath(cacheKey, book.FolderId, "", coverFile);
                book.CoverUrl = File.Exists(cover) ? cover : string.Empty;
            }

            string userDirectory = FileHelper.GetUserDirectory(cacheKey);
            if (!Directory.Exists(userDirectory)) return;
            foreach (string directory in Directory.GetDirectories(userDirectory))
            {
                var info = new DirectoryInfo(directory);
                string folderName = info.Name;
                if (books.Any(book => book.FolderId == folderName)) continue;
                string? jsonFile = Directory.GetFiles(directory, "*_full.json").FirstOrDefault();
                if (jsonFile == null) continue;
                string[] parts = folderName.Split('_');
                var book = new Book
                {
                    Title = parts.Length > 1 && parts[^1].Length >= 8
                        ? string.Join("_", parts.Take(parts.Length - 1)) : folderName,
                    FolderId = folderName,
                    FileName = Path.GetFileName(jsonFile),
                    CoverUrl = Directory.GetFiles(directory, "*.png").FirstOrDefault() ?? string.Empty,
                    IsAvailable = true,
                    CreatedAt = info.CreationTime
                };
                ApplyLocalProgress(book, cacheKey);
                books.Add(book);
            }
        }

        private static void ApplyLocalProgress(Book book, string cacheKey)
        {
            ReadingProgress? progress = ReadingProgressManager.GetProgress(cacheKey, book.FolderId);
            book.LastPage = progress?.CurrentPage ?? 0;
            if (progress != null) book.TotalPageCount = progress.TotalPages;
        }

        private async Task UploadProcess()
        {
            if (_isLoadingLibrary) return;
            LibraryContext context = CurrentContext;
            if (!_mainVM.IsLoggedIn)
            {
                _dialogService.ShowMessage("로그인이 필요합니다.");
                return;
            }
            string? filePath = _filePickerService.PickPdfFile();
            if (string.IsNullOrEmpty(filePath) || !await CheckCopyrightAndDRM(filePath)) return;

            if (!IsCurrent(context)) return;
            string requestId = Guid.NewGuid().ToString();
            var book = new Book
            {
                Title = Path.GetFileNameWithoutExtension(filePath),
                Author = context.Username,
                CreatedAt = DateTime.Now,
                CoverColor = "#DDDDDD",
                JobId = requestId,
                IsBusy = true,
                JobControlsAvailable = true,
                StatusMessage = "전송 중...",
                LastPage = 0,
                TotalPageCount = 0,
                IsAvailable = false
            };
            _allBooks.Insert(0, book);
            RefreshList();
            await SaveLibrary();

            if (!IsCurrent(context)) return;
            UploadResult result = await _libraryService.UploadBookAsync(filePath, context.Username, requestId);
            if (!IsCurrent(context)) return;
            if (!_allBooks.Contains(book))
            {
                if (result.Success && !string.IsNullOrWhiteSpace(result.JobId))
                    await _libraryService.CancelJobAsync(result.JobId);
                return;
            }
            if (!result.Success)
            {
                if (result.Error?.Kind is ApiErrorKind.Network or ApiErrorKind.Timeout or ApiErrorKind.Server)
                {
                    book.StatusMessage = "업로드 접수 여부 확인 중...";
                    await SaveLibrary();
                    StartJobMonitor(book, requestId, JobKind.Analysis, context);
                    return;
                }
                RemoveBookFromLibrary(book);
                await SaveLibrary();
                _dialogService.ShowMessage($"업로드 실패 원인:\n{result.Error?.UserMessage ?? result.Message ?? "요청을 처리하지 못했습니다."}");
                return;
            }
            if (string.IsNullOrWhiteSpace(result.JobId) || string.IsNullOrWhiteSpace(result.BookFolder))
            {
                await HandleFailedJob(book, JobKind.Analysis, "서버가 작업 식별자를 반환하지 않았습니다.");
                return;
            }

            book.FolderId = result.BookFolder;
            book.JobId = result.JobId;
            book.StatusMessage = "서버 분석 대기...";
            await SaveLibrary();
            StartJobMonitor(book, book.JobId, JobKind.Analysis, context);
        }

        private void StartJobMonitor(Book book, string jobId, JobKind kind, LibraryContext context)
        {
            if (string.IsNullOrWhiteSpace(jobId) || !IsCurrent(context)) return;
            var source = new CancellationTokenSource();
            if (!_jobMonitors.TryAdd(jobId, source))
            {
                source.Dispose();
                return;
            }
            _ = MonitorJobAndReleaseAsync(book, jobId, kind, context, source);
        }

        private async Task MonitorJobAndReleaseAsync(Book book, string jobId, JobKind kind,
                                                     LibraryContext context, CancellationTokenSource source)
        {
            try { await MonitorJobAsync(book, jobId, kind, context, source.Token); }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"[Book job {jobId}] {error}");
                if (IsCurrent(context) && !source.IsCancellationRequested)
                    await UpdateStatusAsync(book, "파일을 처리하지 못했습니다. 저장 공간과 접근 권한을 확인해주세요.");
            }
            finally
            {
                ((ICollection<KeyValuePair<string, CancellationTokenSource>>)_jobMonitors)
                    .Remove(new KeyValuePair<string, CancellationTokenSource>(jobId, source));
                source.Dispose();
            }
        }

        private async Task MonitorJobAsync(Book book, string jobId, JobKind kind,
                                           LibraryContext context, CancellationToken cancellationToken)
        {
            int transientFailures = 0;
            int downloadFailures = 0;
            while (IsCurrent(context) && !cancellationToken.IsCancellationRequested)
            {
                ApiResult<JobStatusResponse> result = await _libraryService.GetJobStatusAsync(jobId);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrent(context)) return;
                if (!result.Success)
                {
                    bool submissionMayStillBeCompleting = kind == JobKind.Analysis &&
                        string.IsNullOrWhiteSpace(book.FolderId) &&
                        result.Error?.Kind == ApiErrorKind.NotFound && transientFailures < 8;
                    if (submissionMayStillBeCompleting ||
                        result.Error?.Kind is ApiErrorKind.Network or ApiErrorKind.Timeout or ApiErrorKind.Server)
                    {
                        transientFailures++;
                        await UpdateStatusAsync(book,
                            $"상태 확인 재시도 중 · {result.Error?.UserMessage}");
                        await Task.Delay(RetryDelay(transientFailures), cancellationToken);
                        continue;
                    }
                    await HandleUnrecoverableStatusError(book, kind, result.Error);
                    return;
                }

                transientFailures = 0;
                JobStatusResponse status = result.Value!;
                if (string.IsNullOrWhiteSpace(book.FolderId) && !string.IsNullOrWhiteSpace(status.book_id))
                    book.FolderId = status.book_id;
                switch (status.status)
                {
                    case "queued":
                        if (book.IsCancellationRequested) break;
                        await UpdateStatusAsync(book, kind == JobKind.Analysis
                            ? $"분석 대기 중 · 시도 {status.attempt_count}/{status.max_attempts}"
                            : "AI 음악 생성 대기 중");
                        break;
                    case "running":
                        if (book.IsCancellationRequested) break;
                        await UpdateStatusAsync(book, kind == JobKind.Analysis
                            ? $"책 분석 중 · 시도 {status.attempt_count}/{status.max_attempts}"
                            : "AI 음악 생성 중");
                        break;
                    case "cancel_requested":
                        await MarkCancellationRequested(book);
                        break;
                    case "cancelled":
                        await HandleCancelledJob(book, kind, context.CacheKey);
                        return;
                    case "error":
                    case "skipped":
                        await HandleFailedJob(book, kind, status.error);
                        return;
                    case "done":
                        bool completed = kind == JobKind.Analysis
                            ? await CompleteAnalysisAsync(book, status, context, cancellationToken)
                            : await CompleteMusicAsync(book, context, cancellationToken);
                        if (completed) return;
                        downloadFailures++;
                        await Task.Delay(RetryDelay(downloadFailures), cancellationToken);
                        continue;
                }
                await Task.Delay(TimeSpan.FromSeconds(status.status == "queued" ? 2 : 4), cancellationToken);
            }
        }

        private async Task<bool> CompleteAnalysisAsync(Book book, JobStatusResponse status,
                                                       LibraryContext context, CancellationToken cancellationToken)
        {
            JobResultResponse? artifact = status.result;
            string folder = artifact?.book_folder ?? book.FolderId;
            string textFile = artifact?.text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(textFile))
            {
                await HandleFailedJob(book, JobKind.Analysis, "서버가 본문 산출물 정보를 반환하지 않았습니다.");
                return true;
            }

            string staged = NewStagedBookPath();
            try
            {
                if (!book.IsAvailable) await UpdateStatusAsync(book, "분석 완료 · 본문 다운로드 중...");
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrent(context)) return true;
                ApiResult textResult = await _libraryService.DownloadFileAsync(
                    FileUrl(context.Username, folder, textFile), staged, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!textResult.Success)
                {
                    await UpdateStatusAsync(book, $"본문 다운로드 재시도 중 · {textResult.Error?.UserMessage}");
                    return false;
                }

                string contents = await File.ReadAllTextAsync(staged, cancellationToken);
                List<LocalChapter> chapters = BookContent.ParseChapters(contents);
                cancellationToken.ThrowIfCancellationRequested();
                string destination = FileHelper.GetLocalFilePath(context.CacheKey, folder, "", textFile);
                if (!File.Exists(destination))
                {
                    // Keep a readable first download when optional audio has to be retried.
                    await AtomicFile.WriteAllTextAsync(destination, contents, cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();
                book.FolderId = folder;
                book.FileName = textFile;
                book.Title = artifact?.book_title ?? book.Title;
                if (!string.IsNullOrWhiteSpace(artifact?.author)) book.Author = artifact.author;
                book.IsCancellationRequested = false;
                book.IsBusy = false;
                book.IsAvailable = true;
                await UpdateStatusAsync(book, "본문 준비 완료 · 음악 다운로드 중...");
                cancellationToken.ThrowIfCancellationRequested();
                RefreshList();

                string coverFile = artifact?.cover ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(coverFile))
                {
                    string localCover = FileHelper.GetLocalFilePath(context.CacheKey, folder, "", coverFile);
                    if (File.Exists(localCover) || (await _libraryService.DownloadFileAsync(
                        FileUrl(context.Username, folder, coverFile), localCover, cancellationToken)).Success)
                        book.CoverUrl = localCover;
                }
                cancellationToken.ThrowIfCancellationRequested();
                bool musicDownloaded = await DownloadAllMusicFiles(context.Username, folder, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!musicDownloaded || HasMissingMusic(chapters, context.CacheKey, folder))
                {
                    await UpdateStatusAsync(book, "본문 읽기 가능 · 음악 다운로드 재시도 중");
                    return false;
                }

                File.Move(staged, destination, overwrite: true);
                book.JobId = string.Empty;
                book.MusicJobId = artifact?.music_job_id ?? string.Empty;
                book.StatusMessage = book.HasPendingJob ? "기본 음악 준비 완료 · AI 음악 상태 확인 중" : "완료!";
                await SaveLibrary();
                RefreshList();
                StartJobMonitor(book, book.MusicJobId, JobKind.Music, context);
                return true;
            }
            catch (JsonException)
            {
                await UpdateStatusAsync(book, "서버 본문 형식 오류 · 다시 다운로드 중");
                return false;
            }
            finally { CleanStagedBook(staged); }
        }

        private async Task<bool> CompleteMusicAsync(Book book, LibraryContext context,
                                                    CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(book.FileName) || string.IsNullOrWhiteSpace(book.FolderId))
            {
                await HandleFailedJob(book, JobKind.Music, "로컬 책 정보가 없어 음악을 적용하지 못했습니다.");
                return true;
            }
            await MarkMusicDownloadPending(book);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(context)) return true;
            ApiResult result = await DownloadMusicUpdateAsync(
                context.Username, context.CacheKey, book, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(context)) return true;
            if (!result.Success)
            {
                await UpdateStatusAsync(book, $"AI 음악 업데이트 재시도 중 · {result.Error?.UserMessage}");
                return false;
            }
            book.MusicJobId = string.Empty;
            book.IsCancellationRequested = false;
            book.IsMusicDownloadPending = false;
            book.StatusMessage = "AI 음악 적용 완료";
            await SaveLibrary();
            return true;
        }

        internal async Task<ApiResult> DownloadMusicUpdateAsync(
            string username, string cacheKey, Book book, CancellationToken cancellationToken)
        {
            string destination = FileHelper.GetLocalFilePath(cacheKey, book.FolderId, "", book.FileName);
            string staged = NewStagedBookPath();
            try
            {
                ApiResult json = await _libraryService.DownloadFileAsync(
                    FileUrl(username, book.FolderId, book.FileName), staged, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!json.Success) return json;

                List<LocalChapter> chapters = BookContent.ParseChapters(await File.ReadAllTextAsync(staged, cancellationToken));
                if (!await DownloadAllMusicFiles(username, book.FolderId, cancellationToken))
                    return ApiResult.Fail(new ApiError(ApiErrorKind.Network, "음악 파일을 모두 받지 못했습니다."));

                if (HasMissingMusic(chapters, cacheKey, book.FolderId))
                    return ApiResult.Fail(new ApiError(ApiErrorKind.InvalidResponse, "책이 참조하는 음악 파일이 없습니다."));

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(staged, destination, overwrite: true);
                return ApiResult.Ok();
            }
            catch (Exception error) when (error is JsonException or ArgumentException)
            {
                return ApiResult.Fail(new ApiError(ApiErrorKind.InvalidResponse, error.Message));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return ApiResult.Fail(new ApiError(ApiErrorKind.LocalStorage, error.Message));
            }
            finally { CleanStagedBook(staged); }
        }

        private static bool HasMissingMusic(List<LocalChapter> chapters, string cacheKey, string folder)
            => BookContent.MusicReferences(chapters)
                .Any(reference => BookContent.FindLocalMusic(cacheKey, folder, reference, File.Exists) == null);

        private static string NewStagedBookPath()
        {
            string directory = Path.Combine(FileHelper.BasePath, ".downloads");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        }

        private static void CleanStagedBook(string path)
        {
            try { File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[Book staging cleanup] {error}");
            }
        }

        private async Task HandleCancelledJob(Book book, JobKind kind, string cacheKey)
        {
            book.IsCancellationRequested = false;
            if (kind == JobKind.Analysis)
            {
                book.JobId = string.Empty;
                book.IsBusy = false;
                if (DeleteLocalBookFiles(book, cacheKey)) RemoveBookFromLibrary(book);
                else book.StatusMessage = "분석 취소됨 · 로컬 파일 삭제를 다시 시도해주세요.";
            }
            else
            {
                book.MusicJobId = string.Empty;
                book.IsMusicDownloadPending = false;
                book.StatusMessage = "AI 음악 생성 취소 · 기존 음악 유지";
            }
            CommandManager.InvalidateRequerySuggested();
            await SaveLibrary();
        }

        private async Task HandleFailedJob(Book book, JobKind kind, string? error)
        {
            book.IsCancellationRequested = false;
            if (kind == JobKind.Analysis)
            {
                book.JobId = string.Empty;
                book.IsBusy = false;
                book.IsAvailable = false;
                book.StatusMessage = $"분석 실패 · {error ?? "서버 작업 오류"}";
            }
            else
            {
                book.MusicJobId = string.Empty;
                book.IsMusicDownloadPending = false;
                book.StatusMessage = $"AI 음악 생성 실패 · 기본 음악 사용 · {error ?? "서버 작업 오류"}";
            }
            await SaveLibrary();
        }

        private Task MarkCancellationRequested(Book book)
        {
            book.IsCancellationRequested = true;
            CommandManager.InvalidateRequerySuggested();
            return UpdateStatusAsync(book, "작업 취소 처리 중...");
        }

        private Task MarkMusicDownloadPending(Book book)
        {
            book.IsCancellationRequested = false;
            if (book.IsMusicDownloadPending) return Task.CompletedTask;
            book.IsMusicDownloadPending = true;
            CommandManager.InvalidateRequerySuggested();
            return UpdateStatusAsync(book, "AI 음악 생성 완료 · 다운로드 중...");
        }

        private async Task HandleUnrecoverableStatusError(Book book, JobKind kind, ApiError? error)
        {
            if (error?.Kind == ApiErrorKind.Authentication)
            {
                book.StatusMessage = "로그인 후 작업 상태를 다시 확인합니다.";
                await SaveLibrary();
                return;
            }
            await HandleFailedJob(book, kind, error?.UserMessage);
        }

        private async Task UpdateStatusAsync(Book book, string status)
        {
            if (book.StatusMessage == status) return;
            book.StatusMessage = status;
            await SaveLibrary();
        }

        private static TimeSpan RetryDelay(int failures)
            => TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(failures, 5))));

        private void StopMonitor(string jobId)
        {
            if (_jobMonitors.TryRemove(jobId, out CancellationTokenSource? source))
                source.Cancel();
        }

        private async Task<bool> CheckCopyrightAndDRM(string path)
        {
            bool isPdf = await Task.Run(() =>
            {
                try
                {
                    byte[] buffer = new byte[4];
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                    stream.ReadExactly(buffer, 0, 4);
                    return System.Text.Encoding.ASCII.GetString(buffer) == "%PDF";
                }
                catch { return false; }
            });
            if (!isPdf)
            {
                _dialogService.ShowMessage("올바른 PDF 파일이 아닙니다.");
                return false;
            }
            return _dialogService.ShowConfirm(
                $"파일: {Path.GetFileName(path)}\n\n저작권 문제가 없는 파일이며,\nDRM이 걸려있지 않은 파일입니까?",
                "업로드 확인");
        }

        private void RefreshList()
        {
            IEnumerable<Book> filtered = _allBooks.Where(book =>
                string.IsNullOrWhiteSpace(SearchText) ||
                book.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                book.Author.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            IOrderedEnumerable<Book> sorted = SelectedSortOption switch
            {
                "오래된순" => filtered.OrderBy(book => book.CreatedAt),
                "이름순" => filtered.OrderBy(book => book.Title),
                "이름역순" => filtered.OrderByDescending(book => book.Title),
                "작가이름순" => filtered.OrderBy(book => book.Author),
                "작가이름 역순" => filtered.OrderByDescending(book => book.Author),
                _ => filtered.OrderByDescending(book => book.CreatedAt)
            };
            DisplayBooks.Clear();
            if (string.IsNullOrEmpty(SearchText)) DisplayBooks.Add(new Book { IsAddButton = true });
            foreach (Book book in sorted) DisplayBooks.Add(book);
        }

        private async Task<bool> DownloadAllMusicFiles(string username, string bookFolderId,
                                                          CancellationToken cancellationToken)
        {
            ApiResult<List<string>> listResult = await _libraryService.GetMusicFileListAsync(username, bookFolderId);
            cancellationToken.ThrowIfCancellationRequested();
            if (!listResult.Success) return false;
            foreach (string file in listResult.Value ?? new List<string>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string localPath = FileHelper.GetLocalFilePath(username, bookFolderId, "music", file);
                if (File.Exists(localPath)) continue;
                ApiResult downloadResult = await _libraryService.DownloadFileAsync(
                    FileUrl(username, bookFolderId, "music", file), localPath, cancellationToken);
                if (!downloadResult.Success) return false;
            }
            return true;
        }

        private static string FileUrl(string username, string bookFolder, params string[] segments)
        {
            IEnumerable<string> encoded = new[] { username, bookFolder }.Concat(segments)
                .Select(Uri.EscapeDataString);
            return $"{ApiConfig.BaseUrl}/files/{string.Join("/", encoded)}";
        }
    }
}
