using EBookStudio.Helpers;
using EBookStudio.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace EBookStudio.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
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

        public LibraryViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            DisplayBooks = new ObservableCollection<Book>();
            _allBooks = new List<Book>();

            AddBookCommand = new RelayCommand(
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

            var result = MessageBox.Show($"'{book.Title}' 책을 삭제하시겠습니까?", "삭제 확인",
                                         MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                string username = _mainVM.LoggedInUser;
                // [수정] 정확한 책 폴더 경로 가져오기 (UsersBasePath + username + bookTitle)
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

                _ = Task.Run(async () => await SyncAllBooksInBackground(username));
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

                    // [수정] 평탄화된 경로에서 커버 이미지 찾기
                    string coverPath = Path.Combine(dir, $"{title}.png");
                    // 하위 호환성 체크
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
                MessageBox.Show("로그인이 필요합니다.");
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "PDF 문서 (*.pdf)|*.pdf",
                Title = "책 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
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

                var result = await ApiService.UploadBookAsync(filePath, username);

                if (result.Success)
                {
                    string safeTitle = newBook.Title;

                    // =========================================================
                    // [수정 핵심] 서버 분석 대기 (Polling)
                    // =========================================================
                    newBook.StatusMessage = "AI 분석 중...";
                    bool isReady = false;
                    int retryCount = 0;
                    int maxRetries = 30; // 30초 대기

                    string targetJsonName = result.Text ?? $"{fileName}_full.json";
                    string textUrl = $"{ApiService.BaseUrl}/files/{username}/{fileName}/{targetJsonName}";

                    string targetCoverName = result.Cover ?? $"{fileName}.png";
                    string coverUrl = $"{ApiService.BaseUrl}/files/{username}/{fileName}/{targetCoverName}";

                    while (retryCount < maxRetries)
                    {
                        var checkBytes = await ApiService.DownloadBytesAsync(textUrl);
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
                        MessageBox.Show("서버 분석이 지연되고 있습니다.\n나중에 자동으로 동기화됩니다.");
                        return;
                    }

                    // =========================================================
                    // [다운로드 시작]
                    // =========================================================
                    newBook.StatusMessage = "다운로드 중...";

                    // 1. 커버 이미지 (Root 경로)
                    string localCoverPath = FileHelper.GetLocalFilePath(username, safeTitle, "", "cover.png");
                    bool coverOk = await ApiService.DownloadFileAsync(coverUrl, localCoverPath);

                    // 2. 텍스트 JSON (Root 경로)
                    string localTextPath = FileHelper.GetLocalFilePath(username, safeTitle, "", targetJsonName);
                    await ApiService.DownloadFileAsync(textUrl, localTextPath);

                    // 3. 음악 파일 (공용 폴더)
                    await DownloadAllMusicFiles(username, fileName, safeTitle);

                    // 완료 처리
                    newBook.IsBusy = false;
                    newBook.FileName = targetJsonName;
                    newBook.CoverUrl = coverOk ? localCoverPath : coverUrl;
                    if (!string.IsNullOrEmpty(result.Author)) newBook.Author = result.Author;

                    newBook.StatusMessage = "완료!";
                    newBook.IsAvailable = true;

                    await SaveLibrary();
                    _ = SyncAllBooksInBackground(username);
                }
                else
                {
                    newBook.StatusMessage = "실패";
                    await Task.Delay(1000);
                    _allBooks.Remove(newBook);
                    RefreshList();
                    await SaveLibrary();
                    MessageBox.Show($"업로드 실패 원인:\n{result.Message}");
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
                        fs.Read(buffer, 0, 4);
                    }
                    string header = System.Text.Encoding.ASCII.GetString(buffer);
                    return header == "%PDF";
                }
                catch { return false; }
            });

            if (!isPdf)
            {
                MessageBox.Show("올바른 PDF 파일이 아닙니다.", "형식 오류");
                return false;
            }

            var result = MessageBox.Show(
                $"파일: {Path.GetFileName(path)}\n\n저작권 문제가 없는 파일이며,\nDRM이 걸려있지 않은 파일입니까?",
                "업로드 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
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
            var musicFiles = await ApiService.GetMusicFileListAsync(username, bookId);

            if (musicFiles == null || musicFiles.Count == 0) return;

            // [수정] 음악 공용 폴더 경로 사용
            string tempPath = FileHelper.GetLocalFilePath(username, bookTitle, "music", "temp.wav");
            string localMusicFolder = Path.GetDirectoryName(tempPath)!;

            if (!Directory.Exists(localMusicFolder)) Directory.CreateDirectory(localMusicFolder);

            foreach (var file in musicFiles)
            {
                string localPath = Path.Combine(localMusicFolder, file);

                if (!File.Exists(localPath))
                {
                    string serverUrl = $"{ApiService.BaseUrl}/files/{username}/{bookId}/music/{file}";
                    await ApiService.DownloadFileAsync(serverUrl, localPath);
                }
            }
        }

        public async Task SyncAllBooksInBackground(string username)
        {
            if (string.IsNullOrEmpty(username)) return;

            await Task.Run(async () =>
            {
                string userDir = Path.Combine(FileHelper.UsersBasePath, username);
                if (!Directory.Exists(userDir)) return;

                string[] bookDirs = Directory.GetDirectories(userDir);

                foreach (var bookDir in bookDirs)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(bookDir);
                        string bookTitle = dirInfo.Name;

                        string jsonResponse = await ApiService.GetLatestBookJsonAsync(username, bookTitle);
                        if (!string.IsNullOrEmpty(jsonResponse))
                        {
                            using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                            {
                                if (!doc.RootElement.TryGetProperty("book_data", out var bookData)) continue;

                                string jsonFilename = $"{bookTitle}_full.json";
                                if (doc.RootElement.TryGetProperty("json_filename", out var jsonNameProp))
                                {
                                    jsonFilename = jsonNameProp.GetString() ?? jsonFilename;
                                }

                                // [수정] category="" (루트)
                                string localJsonPath = FileHelper.GetLocalFilePath(username, bookTitle, "", jsonFilename);
                                string newJsonString = bookData.GetRawText();
                                await File.WriteAllTextAsync(localJsonPath, newJsonString);
                            }
                        }

                        await DownloadAllMusicFiles(username, bookTitle, bookTitle);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AutoSync Error] {ex.Message}");
                    }
                }
            });
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