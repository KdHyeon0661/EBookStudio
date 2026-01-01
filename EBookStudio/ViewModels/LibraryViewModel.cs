using EBookStudio.Helpers;
using EBookStudio.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EBookStudio.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;

        // [핵심 변경] 직접 호출 대신 인터페이스 사용
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

            // 의존성 주입 (없으면 실제 서비스 사용)
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
                string username = _mainVM.LoggedInUser;
                string safeUserDir = Path.Combine(FileHelper.UsersBasePath, username, book.Title);

                if (Directory.Exists(safeUserDir))
                {
                    try
                    {
                        Directory.Delete(safeUserDir, true);
                    }
                    catch { }
                }

                _allBooks.Remove(book);
                RefreshList();
                await SaveLibrary();
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

                            var progress = ReadingProgressManager.GetProgress(username, book.Title);

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
                    string title = dirInfo.Name;

                    if (_allBooks.Any(b => b.Title == title)) continue;

                    var newBook = new Book
                    {
                        Title = title,
                        FileName = $"{title}.pdf",
                        IsAvailable = true,
                        CreatedAt = dirInfo.CreationTime
                    };

                    string coverPath = Path.Combine(dir, $"{title}.png");
                    if (!File.Exists(coverPath)) coverPath = Path.Combine(dir, "covers", $"{title}.png");

                    if (File.Exists(coverPath)) newBook.CoverUrl = coverPath;

                    _allBooks.Add(newBook);
                }
            });
        }

        private async Task UploadProcess()
        {
            if (!_mainVM.IsLoggedIn)
            {
                // [수정] MessageBox -> _dialogService
                _dialogService.ShowMessage("로그인이 필요합니다.");
                return;
            }

            // [수정] OpenFileDialog -> _filePickerService
            string? filePath = _filePickerService.PickPdfFile();

            if (!string.IsNullOrEmpty(filePath))
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string username = _mainVM.LoggedInUser;

                bool isValid = await CheckCopyrightAndDRM(filePath);
                if (!isValid) return;

                var newBook = new Book
                {
                    Title = fileName,
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

                // [수정] ApiService -> _libraryService
                var result = await _libraryService.UploadBookAsync(filePath, username);

                if (result.Success)
                {
                    string safeTitle = newBook.Title;

                    newBook.StatusMessage = "AI 분석 중...";
                    bool isReady = false;
                    int retryCount = 0;
                    int maxRetries = 30;

                    string targetJsonName = result.Text ?? $"{fileName}_full.json";
                    string textUrl = $"{ApiService.BaseUrl}/files/{username}/{fileName}/{targetJsonName}";

                    string targetCoverName = result.Cover ?? $"{fileName}.png";
                    string coverUrl = $"{ApiService.BaseUrl}/files/{username}/{fileName}/{targetCoverName}";

                    while (retryCount < maxRetries)
                    {
                        // [수정] ApiService -> _libraryService
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

                    string localCoverPath = FileHelper.GetLocalFilePath(username, safeTitle, "", "cover.png");
                    // [수정] ApiService -> _libraryService
                    bool coverOk = await _libraryService.DownloadFileAsync(coverUrl, localCoverPath);

                    string localTextPath = FileHelper.GetLocalFilePath(username, safeTitle, "", targetJsonName);
                    await _libraryService.DownloadFileAsync(textUrl, localTextPath);

                    await DownloadAllMusicFiles(username, fileName, safeTitle);

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
            // PDF 헤더 체크 로직 (ViewModel에 남겨둠)
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
                // [수정] MessageBox -> _dialogService
                _dialogService.ShowMessage("올바른 PDF 파일이 아닙니다.");
                return false;
            }

            // [수정] MessageBox -> _dialogService.ShowConfirm
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

        private async Task DownloadAllMusicFiles(string username, string bookId, string bookTitle)
        {
            // [수정] ApiService -> _libraryService
            var musicFiles = await _libraryService.GetMusicFileListAsync(username, bookId);

            if (musicFiles == null || musicFiles.Count == 0) return;

            string tempPath = FileHelper.GetLocalFilePath(username, bookTitle, "music", "temp.wav");
            string localMusicFolder = Path.GetDirectoryName(tempPath)!;

            if (!Directory.Exists(localMusicFolder)) Directory.CreateDirectory(localMusicFolder);

            foreach (var file in musicFiles)
            {
                string localPath = Path.Combine(localMusicFolder, file);

                if (!File.Exists(localPath))
                {
                    string serverUrl = $"{ApiService.BaseUrl}/files/{username}/{bookId}/music/{file}";
                    // [수정] ApiService -> _libraryService
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