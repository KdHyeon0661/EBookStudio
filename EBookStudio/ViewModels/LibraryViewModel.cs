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
        public ICommand DeleteBookCommand { get; }
        public ICommand CancelJobCommand { get; }

        public LibraryViewModel(MainViewModel mainVM,
                                ILibraryService? libraryService = null,
                                IDialogService? dialogService = null,
                                IFilePickerService? filePickerService = null)
        {
            _mainVM = mainVM;
            _libraryService = libraryService ?? new LibraryService();
            _dialogService = dialogService ?? new DialogService();
            _filePickerService = filePickerService ?? new FilePickerService();

            AddBookCommand = new AsyncRelayCommand(
                async _ => await UploadProcess(), _ => _mainVM.IsNetworkAvailable);
            OpenBookCommand = new RelayCommand(parameter =>
            {
                if (parameter is Book book && !book.IsAddButton && book.IsAvailable)
                    _mainVM.NavigateToReader(book);
            });
            DeleteBookCommand = new AsyncRelayCommand(async parameter =>
            {
                if (parameter is Book book) await DeleteBook(book);
            });
            CancelJobCommand = new AsyncRelayCommand(async parameter =>
            {
                if (parameter is Book book) await CancelProcessing(book);
            });
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

        private async Task DeleteBook(Book book)
        {
            if (!_dialogService.ShowConfirm($"'{book.Title}' 책을 삭제하시겠습니까?", "삭제 확인"))
                return;

            await CancelRemoteJobsBestEffort(book);
            RemoveBookFromLibrary(book);
            await SaveLibrary();
            await Task.Delay(100);
            DeleteLocalBookFiles(book);
        }

        private async Task CancelProcessing(Book book)
        {
            if (!book.HasPendingJob) return;
            if (!_dialogService.ShowConfirm($"'{book.Title}'의 백그라운드 작업을 취소하시겠습니까?", "작업 취소"))
                return;

            string[] jobIds = new[] { book.JobId, book.MusicJobId }
                .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
            foreach (string jobId in jobIds)
            {
                ApiResult<JobStatusResponse> result = await _libraryService.CancelJobAsync(jobId);
                if (!result.Success && result.Error?.Kind != ApiErrorKind.NotFound)
                {
                    _dialogService.ShowMessage($"작업 취소 실패: {result.Error?.UserMessage}");
                    return;
                }
            }

            bool analysisWasPending = !string.IsNullOrWhiteSpace(book.JobId);
            foreach (string jobId in jobIds) StopMonitor(jobId);
            book.JobId = string.Empty;
            book.MusicJobId = string.Empty;
            book.IsBusy = false;
            if (analysisWasPending)
            {
                RemoveBookFromLibrary(book);
                DeleteLocalBookFiles(book);
            }
            else
            {
                book.StatusMessage = "AI 음악 생성 취소 · 기본 음악 사용";
            }
            await SaveLibrary();
        }

        private async Task CancelRemoteJobsBestEffort(Book book)
        {
            foreach (string jobId in new[] { book.JobId, book.MusicJobId }
                         .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                StopMonitor(jobId);
                if (!_mainVM.IsLoggedIn || !_mainVM.IsNetworkAvailable) continue;
                ApiResult<JobStatusResponse> result = await _libraryService.CancelJobAsync(jobId);
                if (!result.Success)
                    System.Diagnostics.Debug.WriteLine($"작업 취소 실패 {jobId}: {result.Error?.Message}");
            }
        }

        private void RemoveBookFromLibrary(Book book)
        {
            _allBooks.Remove(book);
            RefreshList();
        }

        private void DeleteLocalBookFiles(Book book)
        {
            if (string.IsNullOrWhiteSpace(book.FolderId)) return;
            string directory = FileHelper.GetBookDirectory(_mainVM.LoggedInUser, book.FolderId);
            if (!Directory.Exists(directory)) return;
            try { Directory.Delete(directory, recursive: true); }
            catch (Exception error) { _dialogService.ShowMessage($"로컬 책 파일 삭제 실패: {error.Message}"); }
        }

        private async Task SaveLibrary()
        {
            await _saveLock.WaitAsync();
            try
            {
                string username = _mainVM.LoggedInUser;
                if (string.IsNullOrEmpty(username)) return;
                string path = FileHelper.GetLibraryFilePath(username);
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
            StopMonitoring();
            try
            {
                string username = _mainVM.LoggedInUser;
                if (string.IsNullOrEmpty(username))
                {
                    _allBooks.Clear();
                    RefreshList();
                    return;
                }

                string path = FileHelper.GetLibraryFilePath(username);
                _allBooks.Clear();
                if (File.Exists(path))
                {
                    string json = await File.ReadAllTextAsync(path);
                    List<Book>? loadedBooks = JsonSerializer.Deserialize<List<Book>>(json);
                    if (loadedBooks != null)
                    {
                        foreach (Book book in loadedBooks)
                        {
                            if (!string.IsNullOrWhiteSpace(book.JobId))
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

                            if (!string.IsNullOrWhiteSpace(book.FolderId))
                            {
                                ReadingProgress? progress = ReadingProgressManager.GetProgress(username, book.FolderId);
                                if (progress != null)
                                {
                                    book.LastPage = progress.CurrentPage;
                                    book.TotalPageCount = progress.TotalPages;
                                }
                            }
                        }
                        _allBooks.AddRange(loadedBooks);
                    }
                }

                await ScanLocalFolders(username);
                RefreshList();
                if (_mainVM.IsLoggedIn)
                {
                    foreach (Book book in _allBooks.ToList())
                    {
                        if (!string.IsNullOrWhiteSpace(book.JobId))
                            StartJobMonitor(book, book.JobId, JobKind.Analysis, username);
                        if (!string.IsNullOrWhiteSpace(book.MusicJobId))
                            StartJobMonitor(book, book.MusicJobId, JobKind.Music, username);
                    }
                }
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"로드 실패: {error}");
                _dialogService.ShowMessage("로컬 보관함 정보를 읽지 못했습니다. 책 폴더를 다시 검색합니다.");
                await ScanLocalFolders(_mainVM.LoggedInUser);
                RefreshList();
            }
        }

        public async Task ImportDownloadedBook(ServerBookItem item)
        {
            string username = _mainVM.LoggedInUser;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(item.Folder)) return;
            string localText = FileHelper.GetLocalFilePath(username, item.Folder, string.Empty, item.TextFile);
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
            string localCover = FileHelper.GetLocalFilePath(username, item.Folder, string.Empty, item.CoverFile);
            book.CoverUrl = File.Exists(localCover) ? localCover : string.Empty;
            book.IsBusy = false;
            book.IsAvailable = true;
            book.StatusMessage = "서버 보관함에서 다운로드 완료";
            await SaveLibrary();
            RefreshList();
        }

        private async Task ScanLocalFolders(string username)
        {
            await Task.Run(() =>
            {
                string userDirectory = FileHelper.GetUserDirectory(username);
                if (!Directory.Exists(userDirectory)) return;
                foreach (string directory in Directory.GetDirectories(userDirectory))
                {
                    var info = new DirectoryInfo(directory);
                    string folderName = info.Name;
                    string displayTitle = folderName;
                    string[] parts = folderName.Split('_');
                    if (parts.Length > 1 && parts[^1].Length >= 8)
                        displayTitle = string.Join("_", parts.Take(parts.Length - 1));
                    if (_allBooks.Any(book => book.FolderId == folderName)) continue;

                    string jsonFile = Directory.GetFiles(directory, "*_full.json").FirstOrDefault() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(jsonFile)) continue;
                    var book = new Book
                    {
                        Title = displayTitle,
                        FolderId = folderName,
                        FileName = Path.GetFileName(jsonFile),
                        IsAvailable = true,
                        CreatedAt = info.CreationTime
                    };
                    string coverPath = Directory.GetFiles(directory, "*.png").FirstOrDefault() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(coverPath)) book.CoverUrl = coverPath;
                    _allBooks.Add(book);
                }
            });
        }

        private async Task UploadProcess()
        {
            if (!_mainVM.IsLoggedIn)
            {
                _dialogService.ShowMessage("로그인이 필요합니다.");
                return;
            }
            string? filePath = _filePickerService.PickPdfFile();
            if (string.IsNullOrEmpty(filePath) || !await CheckCopyrightAndDRM(filePath)) return;

            string username = _mainVM.LoggedInUser;
            string requestId = Guid.NewGuid().ToString();
            var book = new Book
            {
                Title = Path.GetFileNameWithoutExtension(filePath),
                Author = username,
                CreatedAt = DateTime.Now,
                CoverColor = "#DDDDDD",
                JobId = requestId,
                IsBusy = true,
                StatusMessage = "전송 중...",
                LastPage = 0,
                TotalPageCount = 0,
                IsAvailable = false
            };
            _allBooks.Insert(0, book);
            RefreshList();
            await SaveLibrary();

            UploadResult result = await _libraryService.UploadBookAsync(filePath, username, requestId);
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
                    StartJobMonitor(book, requestId, JobKind.Analysis, username);
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
            StartJobMonitor(book, book.JobId, JobKind.Analysis, username);
        }

        private void StartJobMonitor(Book book, string jobId, JobKind kind, string username)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return;
            var source = new CancellationTokenSource();
            if (!_jobMonitors.TryAdd(jobId, source))
            {
                source.Dispose();
                return;
            }
            _ = MonitorJobAndReleaseAsync(book, jobId, kind, username, source);
        }

        private async Task MonitorJobAndReleaseAsync(Book book, string jobId, JobKind kind,
                                                     string username, CancellationTokenSource source)
        {
            try { await MonitorJobAsync(book, jobId, kind, username, source.Token); }
            catch (OperationCanceledException) { }
            finally
            {
                ((ICollection<KeyValuePair<string, CancellationTokenSource>>)_jobMonitors)
                    .Remove(new KeyValuePair<string, CancellationTokenSource>(jobId, source));
                source.Dispose();
            }
        }

        private async Task MonitorJobAsync(Book book, string jobId, JobKind kind,
                                           string username, CancellationToken cancellationToken)
        {
            int transientFailures = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                ApiResult<JobStatusResponse> result = await _libraryService.GetJobStatusAsync(jobId);
                cancellationToken.ThrowIfCancellationRequested();
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
                        await UpdateStatusAsync(book, kind == JobKind.Analysis
                            ? $"분석 대기 중 · 시도 {status.attempt_count}/{status.max_attempts}"
                            : "AI 음악 생성 대기 중");
                        break;
                    case "running":
                        await UpdateStatusAsync(book, kind == JobKind.Analysis
                            ? $"책 분석 중 · 시도 {status.attempt_count}/{status.max_attempts}"
                            : "AI 음악 생성 중");
                        break;
                    case "cancel_requested":
                        await UpdateStatusAsync(book, "작업 취소 처리 중...");
                        break;
                    case "cancelled":
                        await HandleCancelledJob(book, kind);
                        return;
                    case "error":
                    case "skipped":
                        await HandleFailedJob(book, kind, status.error);
                        return;
                    case "done":
                        bool completed = kind == JobKind.Analysis
                            ? await CompleteAnalysisAsync(book, status, username, cancellationToken)
                            : await CompleteMusicAsync(book, username, cancellationToken);
                        if (completed) return;
                        transientFailures++;
                        await Task.Delay(RetryDelay(transientFailures), cancellationToken);
                        continue;
                }
                await Task.Delay(TimeSpan.FromSeconds(status.status == "queued" ? 2 : 4), cancellationToken);
            }
        }

        private async Task<bool> CompleteAnalysisAsync(Book book, JobStatusResponse status,
                                                       string username, CancellationToken cancellationToken)
        {
            JobResultResponse? artifact = status.result;
            string folder = artifact?.book_folder ?? book.FolderId;
            string textFile = artifact?.text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(textFile))
            {
                await HandleFailedJob(book, JobKind.Analysis, "서버가 본문 산출물 정보를 반환하지 않았습니다.");
                return true;
            }

            await UpdateStatusAsync(book, "분석 완료 · 파일 다운로드 중...");
            ApiResult textResult = await _libraryService.DownloadFileAsync(
                FileUrl(username, folder, textFile),
                FileHelper.GetLocalFilePath(username, folder, string.Empty, textFile));
            cancellationToken.ThrowIfCancellationRequested();
            if (!textResult.Success)
            {
                await UpdateStatusAsync(book, $"본문 다운로드 재시도 중 · {textResult.Error?.UserMessage}");
                return false;
            }

            bool coverDownloaded = false;
            string coverFile = artifact?.cover ?? string.Empty;
            string localCover = string.Empty;
            if (!string.IsNullOrWhiteSpace(coverFile))
            {
                localCover = FileHelper.GetLocalFilePath(username, folder, string.Empty, coverFile);
                ApiResult coverResult = await _libraryService.DownloadFileAsync(
                    FileUrl(username, folder, coverFile), localCover);
                coverDownloaded = coverResult.Success;
            }
            cancellationToken.ThrowIfCancellationRequested();
            bool musicDownloaded = await DownloadAllMusicFiles(username, folder);
            cancellationToken.ThrowIfCancellationRequested();

            book.FolderId = folder;
            book.FileName = textFile;
            book.Title = artifact?.book_title ?? book.Title;
            if (!string.IsNullOrWhiteSpace(artifact?.author)) book.Author = artifact.author;
            book.CoverUrl = coverDownloaded ? localCover : string.Empty;
            book.JobId = string.Empty;
            book.MusicJobId = artifact?.music_job_id ?? string.Empty;
            book.IsBusy = false;
            book.IsAvailable = true;
            book.StatusMessage = !string.IsNullOrWhiteSpace(book.MusicJobId)
                ? "기본 음악 준비 완료 · AI 음악 생성 중"
                : "완료!";
            if (!coverDownloaded) book.StatusMessage += " · 표지 없음";
            if (!musicDownloaded) book.StatusMessage += " · 음악 일부 재시도 필요";
            await SaveLibrary();
            RefreshList();
            if (!string.IsNullOrWhiteSpace(book.MusicJobId))
                StartJobMonitor(book, book.MusicJobId, JobKind.Music, username);
            return true;
        }

        private async Task<bool> CompleteMusicAsync(Book book, string username,
                                                    CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(book.FileName) || string.IsNullOrWhiteSpace(book.FolderId))
            {
                await HandleFailedJob(book, JobKind.Music, "로컬 책 정보가 없어 음악을 적용하지 못했습니다.");
                return true;
            }
            await UpdateStatusAsync(book, "AI 음악 생성 완료 · 다운로드 중...");
            ApiResult jsonResult = await _libraryService.DownloadFileAsync(
                FileUrl(username, book.FolderId, book.FileName),
                FileHelper.GetLocalFilePath(username, book.FolderId, string.Empty, book.FileName));
            cancellationToken.ThrowIfCancellationRequested();
            if (!jsonResult.Success)
            {
                await UpdateStatusAsync(book, $"갱신된 본문 다운로드 재시도 중 · {jsonResult.Error?.UserMessage}");
                return false;
            }
            if (!await DownloadAllMusicFiles(username, book.FolderId))
            {
                await UpdateStatusAsync(book, "AI 음악 파일 다운로드 재시도 중...");
                return false;
            }
            cancellationToken.ThrowIfCancellationRequested();
            book.MusicJobId = string.Empty;
            book.StatusMessage = "AI 음악 적용 완료";
            await SaveLibrary();
            return true;
        }

        private async Task HandleCancelledJob(Book book, JobKind kind)
        {
            if (kind == JobKind.Analysis)
            {
                book.JobId = string.Empty;
                book.IsBusy = false;
                RemoveBookFromLibrary(book);
                DeleteLocalBookFiles(book);
            }
            else
            {
                book.MusicJobId = string.Empty;
                book.StatusMessage = "AI 음악 생성 취소 · 기본 음악 사용";
            }
            await SaveLibrary();
        }

        private async Task HandleFailedJob(Book book, JobKind kind, string? error)
        {
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
                book.StatusMessage = $"AI 음악 생성 실패 · 기본 음악 사용 · {error ?? "서버 작업 오류"}";
            }
            await SaveLibrary();
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

        private async Task<bool> DownloadAllMusicFiles(string username, string bookFolderId)
        {
            ApiResult<List<string>> listResult = await _libraryService.GetMusicFileListAsync(username, bookFolderId);
            if (!listResult.Success) return false;
            foreach (string file in listResult.Value ?? new List<string>())
            {
                string localPath = FileHelper.GetLocalFilePath(username, bookFolderId, "music", file);
                if (File.Exists(localPath)) continue;
                ApiResult downloadResult = await _libraryService.DownloadFileAsync(
                    FileUrl(username, bookFolderId, "music", file), localPath);
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