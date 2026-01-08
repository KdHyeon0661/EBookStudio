using EBookStudio.Helpers;
using EBookStudio.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace EBookStudio.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private readonly ILibraryService _libraryService;
        private readonly IDialogService _dialogService;
        private readonly IFilePickerService _filePickerService;

        private List<Book> _allBooks;
        public ObservableCollection<Book> DisplayBooks { get; private set; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                RefreshList();
            }
        }

        private string _selectedSortOption = "최신생성순";
        public List<string> SortOptions { get; } = new List<string>
        {
            "최신생성순", "오래된순", "이름순", "이름역순", "작가이름순", "작가이름 역순"
        };

        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                _selectedSortOption = value;
                OnPropertyChanged();
                RefreshList();
            }
        }

        public ICommand AddBookCommand { get; }
        public ICommand OpenBookCommand { get; }
        public ICommand DeleteBookCommand { get; private set; }

        public LibraryViewModel(MainViewModel mainVM,
                        ILibraryService? libraryService = null,
                        IDialogService? dialogService = null,
                        IFilePickerService? filePickerService = null)
        {
            _mainVM = mainVM;

            _libraryService = libraryService ?? new LibraryService();
            _dialogService = dialogService ?? new DialogService();
            _filePickerService = filePickerService ?? new FilePickerService();

            DisplayBooks = new ObservableCollection<Book>();
            _allBooks = new List<Book>();

            AddBookCommand = new AsyncRelayCommand(
                execute: async o => await UploadProcess(),
                canExecute: o => _mainVM.IsNetworkAvailable
            );

            OpenBookCommand = new RelayCommand(o =>
            {
                if (o is Book selectedBook && !selectedBook.IsAddButton)
                {
                    _mainVM.NavigateToReader(selectedBook);
                }
            });

            DeleteBookCommand = new RelayCommand(param => DeleteBook((Book)param));

            RefreshList();
        }

        private async void DeleteBook(Book book)
        {
            if (book == null) return;

            bool isConfirmed = _dialogService.ShowConfirm(
                $"'{book.Title}' 책을 삭제하시겠습니까?",
                "삭제 확인");

            if (isConfirmed)
            {
                // 1. 화면(리스트)에서 먼저 지웁니다. (이미지가 사라져야 파일 잠금이 풀림)
                _allBooks.Remove(book);
                RefreshList();
                await SaveLibrary();

                // 2. 이미지가 완전히 해제될 때까지 0.5초만 기다립니다.
                await Task.Delay(500);

                // 3. 이제 안전하게 폴더를 삭제합니다.
                string username = _mainVM.LoggedInUser;
                string folderName = !string.IsNullOrEmpty(book.FolderId) ? book.FolderId : book.Title;
                string safeUserDir = Path.Combine(FileHelper.UsersBasePath, username, folderName);

                if (Directory.Exists(safeUserDir))
                {
                    try
                    {
                        Directory.Delete(safeUserDir, true);
                    }
                    catch
                    {
                        // 혹시라도 실패하면 무시 (다음번 실행 때 정리됨)
                    }
                }
            }
        }

        private async Task SaveLibrary()
        {
            try
            {
                string username = _mainVM.LoggedInUser;
                if (string.IsNullOrEmpty(username)) return;

                string path = FileHelper.GetLibraryFilePath(username);
                var booksToSave = _allBooks.Where(b => !b.IsAddButton).ToList();

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(booksToSave, options);

                await File.WriteAllTextAsync(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"저장 실패: {ex.Message}");
            }
        }

        public async Task LoadLibrary()
        {
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
                    var loadedBooks = JsonSerializer.Deserialize<List<Book>>(json);

                    if (loadedBooks != null)
                    {
                        loadedBooks.RemoveAll(b => b.IsBusy);

                        foreach (var book in loadedBooks)
                        {
                            if (book.IsBusy)
                            {
                                book.IsBusy = false;
                                book.StatusMessage = "업로드 중단됨";
                                book.IsAvailable = false;
                            }

                            var progress = ReadingProgressManager.GetProgress(username, book.FolderId);

                            if (progress != null)
                            {
                                book.LastPage = progress.CurrentPage;
                                book.TotalPageCount = progress.TotalPages;
                            }
                        }

                        _allBooks.AddRange(loadedBooks);
                    }
                }

                await ScanLocalFolders(username);
                RefreshList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"로드 실패: {ex.Message}");
            }
        }

        private async Task ScanLocalFolders(string username)
        {
            await Task.Run(() =>
            {
                string userDir = Path.Combine(FileHelper.UsersBasePath, username);
                if (!Directory.Exists(userDir)) return;

                string[] bookDirs = Directory.GetDirectories(userDir);
                foreach (var dir in bookDirs)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    string folderName = dirInfo.Name; // 실제 폴더명 (예: 소나기_uuid)

                    // 폴더명에서 UUID 제거하고 제목만 추출 (표시용)
                    string displayTitle = folderName;
                    if (folderName.Contains("_"))
                    {
                        var parts = folderName.Split('_');
                        // 마지막 부분이 UUID(8자리 이상)라고 가정
                        if (parts.Length > 1 && parts.Last().Length >= 8)
                        {
                            displayTitle = string.Join("_", parts.Take(parts.Length - 1));
                        }
                    }

                    // 이미 리스트에 있는지 확인 (FolderId 기준)
                    if (_allBooks.Any(b => b.FolderId == folderName)) continue;

                    var newBook = new Book
                    {
                        Title = displayTitle,      // 화면용
                        FolderId = folderName,     // [중요] 실제 폴더명
                        FileName = $"{displayTitle}.json", // (추정)
                        IsAvailable = true,
                        CreatedAt = dirInfo.CreationTime
                    };

                    string coverPath = Path.Combine(dir, $"{folderName}.png");
                    // 구버전 호환 (폴더명.png가 없으면 제목.png 시도)
                    if (!File.Exists(coverPath)) coverPath = Path.Combine(dir, $"{displayTitle}.png");

                    if (File.Exists(coverPath)) newBook.CoverUrl = coverPath;

                    _allBooks.Add(newBook);
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

            if (!string.IsNullOrEmpty(filePath))
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string username = _mainVM.LoggedInUser;

                bool isValid = await CheckCopyrightAndDRM(filePath);
                if (!isValid) return;

                var newBook = new Book
                {
                    Title = fileName, // 일단 파일명으로 제목 설정
                    Author = username,
                    CreatedAt = DateTime.Now,
                    CoverColor = "#DDDDDD",
                    IsBusy = true,
                    StatusMessage = "전송 중...",
                    LastPage = 0,
                    TotalPageCount = 0,
                    IsAvailable = false
                };

                _allBooks.Insert(0, newBook);
                RefreshList();
                await SaveLibrary();

                await Task.Delay(500);
                newBook.StatusMessage = "서버 분석 대기...";

                var result = await _libraryService.UploadBookAsync(filePath, username);

                if (result.Success)
                {
                    // [중요] 서버가 정해준 FolderId 저장
                    newBook.FolderId = result.BookFolder ?? newBook.Title;
                    newBook.Title = result.BookTitle ?? newBook.Title; // 서버에서 정제된 제목이 오면 반영

                    string safeFolderId = newBook.FolderId; // 실제 폴더명 사용

                    newBook.StatusMessage = "AI 분석 중...";
                    bool isReady = false;
                    int retryCount = 0;
                    int maxRetries = 30;

                    // URL 요청 시에는 실제 FolderId 사용
                    string targetJsonName = result.Text ?? $"{newBook.Title}_full.json";
                    string textUrl = $"{ApiConfig.BaseUrl}/files/{username}/{safeFolderId}/{targetJsonName}";

                    // 커버 이미지도 FolderId 기준일 수 있음 (서버 로직에 따라 다름)
                    // 보통은 제목.png 지만 안전하게 result.Cover 사용
                    string targetCoverName = result.Cover ?? $"{safeFolderId}.png";
                    string coverUrl = $"{ApiConfig.BaseUrl}/files/{username}/{safeFolderId}/{targetCoverName}";

                    while (retryCount < maxRetries)
                    {
                        var checkBytes = await _libraryService.DownloadBytesAsync(textUrl);
                        if (checkBytes != null && checkBytes.Length > 0)
                        {
                            isReady = true;
                            break;
                        }
                        retryCount++;
                        newBook.StatusMessage = $"AI 분석 중... ({retryCount}s)";
                        await Task.Delay(1000);
                    }

                    if (!isReady)
                    {
                        newBook.StatusMessage = "분석 시간 초과";
                        newBook.IsBusy = false;
                        _dialogService.ShowMessage("서버 분석이 지연되고 있습니다.\n나중에 자동으로 동기화됩니다.");
                        return;
                    }

                    newBook.StatusMessage = "다운로드 중...";

                    // [수정] 저장 경로 생성 시 safeFolderId 사용
                    string localCoverPath = FileHelper.GetLocalFilePath(username, safeFolderId, "", targetCoverName);
                    bool coverOk = await _libraryService.DownloadFileAsync(coverUrl, localCoverPath);

                    string localTextPath = FileHelper.GetLocalFilePath(username, safeFolderId, "", targetJsonName);
                    await _libraryService.DownloadFileAsync(textUrl, localTextPath);

                    // [수정] 음악 다운로드 시 safeFolderId 전달
                    await DownloadAllMusicFiles(username, safeFolderId);

                    newBook.IsBusy = false;
                    newBook.FileName = targetJsonName;
                    newBook.CoverUrl = coverOk ? localCoverPath : coverUrl;
                    if (!string.IsNullOrEmpty(result.Author)) newBook.Author = result.Author;

                    newBook.StatusMessage = "완료!";
                    newBook.IsAvailable = true;

                    await SaveLibrary();
                }
                else
                {
                    newBook.StatusMessage = "실패";
                    await Task.Delay(1000);
                    _allBooks.Remove(newBook);
                    RefreshList();
                    await SaveLibrary();
                    _dialogService.ShowMessage($"업로드 실패 원인:\n{result.Message}");
                }
            }
        }

        private async Task<bool> CheckCopyrightAndDRM(string path)
        {
            bool isPdf = await Task.Run(() =>
            {
                try
                {
                    byte[] buffer = new byte[4];
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        fs.ReadExactly(buffer, 0, 4);
                    }
                    string header = System.Text.Encoding.ASCII.GetString(buffer);
                    return header == "%PDF";
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
            var filtered = _allBooks.Where(b =>
                string.IsNullOrWhiteSpace(SearchText) ||
                b.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                b.Author.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            IOrderedEnumerable<Book> sorted;
            switch (SelectedSortOption)
            {
                case "오래된순": sorted = filtered.OrderBy(b => b.CreatedAt); break;
                case "이름순": sorted = filtered.OrderBy(b => b.Title); break;
                case "이름역순": sorted = filtered.OrderByDescending(b => b.Title); break;
                case "작가이름순": sorted = filtered.OrderBy(b => b.Author); break;
                case "작가이름 역순": sorted = filtered.OrderByDescending(b => b.Author); break;
                case "최신생성순":
                default: sorted = filtered.OrderByDescending(b => b.CreatedAt); break;
            }

            DisplayBooks.Clear();
            if (string.IsNullOrEmpty(SearchText))
            {
                DisplayBooks.Add(new Book { IsAddButton = true });
            }

            foreach (var book in sorted)
            {
                DisplayBooks.Add(book);
            }
        }

        // [수정] 인자명을 bookId -> bookFolderId로 변경
        private async Task DownloadAllMusicFiles(string username, string bookFolderId)
        {
            var musicFiles = await _libraryService.GetMusicFileListAsync(username, bookFolderId);

            if (musicFiles == null || musicFiles.Count == 0) return;

            // [수정] FileHelper 호출 시 bookFolderId 전달
            string tempPath = FileHelper.GetLocalFilePath(username, bookFolderId, "music", "temp.wav");
            string localMusicFolder = Path.GetDirectoryName(tempPath)!;

            if (!Directory.Exists(localMusicFolder)) Directory.CreateDirectory(localMusicFolder);

            foreach (var file in musicFiles)
            {
                string localPath = Path.Combine(localMusicFolder, file);

                if (!File.Exists(localPath))
                {
                    // URL에도 bookFolderId 사용
                    string serverUrl = $"{ApiConfig.BaseUrl}/files/{username}/{bookFolderId}/music/{file}";
                    await _libraryService.DownloadFileAsync(serverUrl, localPath);
                }
            }
        }

        private bool IsSafeMusicFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            if (!fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return false;
            if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\")) return false;
            if (!Regex.IsMatch(fileName, @"^[a-zA-Z0-9_.-]+$")) return false;
            return true;
        }
    }
}