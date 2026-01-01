using EBookStudio.Helpers;
using EBookStudio.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EBookStudio.ViewModels
{
    public class ReadBookViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private readonly Book _currentBook;
        private readonly string _username;

        private readonly IBookFileSystem _fileSystem;
        private readonly INoteService _noteService;

        public string CurrentUser => _username;
        public int TargetPage { get; set; } = 1;

        private List<string> _allPages = new List<string>();
        private List<int> _pageToChapterMap = new List<int>();
        private Dictionary<int, int> _chapterStartPageMap = new Dictionary<int, int>();

        private readonly MediaPlayer _mediaPlayer = new MediaPlayer();
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private string _currentPlayingMusic = string.Empty;
        private List<string> _pageToMusicMap = new List<string>();

        private bool _isTimerUpdating = false;

        private bool _isMusicPlaying;
        public bool IsMusicPlaying
        {
            get => _isMusicPlaying;
            set
            {
                if (_isMusicPlaying != value)
                {
                    _isMusicPlaying = value;
                    OnPropertyChanged();

                    if (_isMusicPlaying)
                    {
                        if (_isMusicEnabled)
                        {
                            _mediaPlayer.Play();
                            _timer.Start();
                        }
                    }
                    else
                    {
                        _mediaPlayer.Pause();
                        _timer.Stop();
                    }
                }
            }
        }

        private bool _isMusicEnabled = true;
        public bool IsMusicEnabled
        {
            get => _isMusicEnabled;
            set
            {
                _isMusicEnabled = value;
                OnPropertyChanged();

                if (!value) IsMusicPlaying = false;
                else UpdateMusicPlayback();
            }
        }

        private double _currentPosition;
        public double CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (_currentPosition != value)
                {
                    _currentPosition = value;
                    OnPropertyChanged();
                    if (!_isTimerUpdating)
                    {
                        _mediaPlayer.Position = TimeSpan.FromSeconds(_currentPosition);
                    }
                }
            }
        }

        private double _totalDuration = 1;
        public double TotalDuration
        {
            get => _totalDuration;
            set { _totalDuration = value; OnPropertyChanged(); }
        }

        public string BookTitle => _currentBook?.Title ?? "제목 없음";

        private string _currentPageContent = "로딩 중...";
        public string CurrentPageContent
        {
            get => _currentPageContent;
            set { _currentPageContent = value; OnPropertyChanged(); }
        }

        private int _currentPageNum = 1;
        public int CurrentPageNum
        {
            get => _currentPageNum;
            set
            {
                if (value < 1) value = 1;
                if (_allPages.Count > 0 && value > _allPages.Count) value = _allPages.Count;

                if (_currentPageNum != value)
                {
                    _currentPageNum = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PageStatus));
                    if (_currentBook != null) _currentBook.LastPage = _currentPageNum;

                    UpdateDisplayContent();
                    CheckCurrentPageStatus();
                    UpdateMusicPlayback();

                    if (TotalPages > 0 && _currentBook != null)
                    {
                        ReadingProgressManager.SaveProgress(_username, _currentBook.Title, _currentPageNum, TotalPages);
                    }
                }
            }
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            set
            {
                _totalPages = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageStatus));
                if (_currentBook != null) _currentBook.TotalPageCount = _totalPages;
            }
        }

        public string PageStatus => $"{CurrentPageNum} / {TotalPages}";

        private bool _isBookmarked;
        public bool IsBookmarked { get => _isBookmarked; set { _isBookmarked = value; OnPropertyChanged(); } }

        private bool _isMenuVisible = false;
        public bool IsMenuVisible { get => _isMenuVisible; set { _isMenuVisible = value; OnPropertyChanged(); } }

        private bool _isTocVisible = false;
        public bool IsTocVisible { get => _isTocVisible; set { _isTocVisible = value; OnPropertyChanged(); } }

        public ObservableCollection<string> TableOfContents { get; } = new ObservableCollection<string>();

        private int _selectedChapterIndex = -1;
        public int SelectedChapterIndex
        {
            get => _selectedChapterIndex;
            set
            {
                if (_selectedChapterIndex != value)
                {
                    _selectedChapterIndex = value;
                    OnPropertyChanged();
                    if (value >= 0 && _chapterStartPageMap.ContainsKey(value))
                    {
                        int targetPage = _chapterStartPageMap[value];
                        if (CurrentPageNum != targetPage)
                        {
                            CurrentPageNum = targetPage;
                            IsMenuVisible = false;
                            IsTocVisible = false;
                        }
                    }
                }
            }
        }

        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand ToggleMenuCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ToggleMusicCommand { get; }
        public ICommand ToggleTocCommand { get; }
        public ICommand OpenNoteCommand { get; }
        public ICommand OpenSettingCommand { get; }
        public ICommand ToggleBookmarkCommand { get; }

        // [수정] 생성자에 인터페이스 파라미터 추가 (변수명 그대로 유지)
        public ReadBookViewModel(MainViewModel mainVM, Book book, IBookFileSystem? fileSystem = null, INoteService? noteService = null)
        {
            _mainVM = mainVM;
            _currentBook = book;
            _username = mainVM.LoggedInUser;

            // [추가] 서비스 초기화
            _fileSystem = fileSystem ?? new BookFileSystem();
            _noteService = noteService ?? new NoteService();

            NextPageCommand = new RelayCommand(o => { if (CurrentPageNum < TotalPages) CurrentPageNum++; });
            PrevPageCommand = new RelayCommand(o => { if (CurrentPageNum > 1) CurrentPageNum--; });
            ToggleMenuCommand = new RelayCommand(o => { IsMenuVisible = !IsMenuVisible; if (!IsMenuVisible) IsTocVisible = false; });

            CloseCommand = new RelayCommand(o =>
            {
                _mediaPlayer.Stop();
                _timer.Stop();
                _mainVM.NavigateToHome();
            });

            ToggleMusicCommand = new RelayCommand(o => IsMusicPlaying = !IsMusicPlaying);
            ToggleTocCommand = new RelayCommand(o => IsTocVisible = !IsTocVisible);
            OpenNoteCommand = new RelayCommand(o => { _mainVM.CurrentView = new NoteViewModel(_mainVM, _currentBook, CurrentPageNum); });
            OpenSettingCommand = new RelayCommand(o => { });

            ToggleBookmarkCommand = new RelayCommand(o =>
            {
                IsBookmarked = !IsBookmarked;
                var item = new NoteItem { Type = "Bookmark", PageNumber = CurrentPageNum, Content = $"p.{CurrentPageNum} - {DateTime.Now:yyyy.MM.dd}", CreatedAt = DateTime.Now };

                // [수정] NoteManager 직접 호출 대신 _noteService 사용
                if (IsBookmarked) _noteService.AddItem(_username, _currentBook.Title, item);
                else _noteService.RemoveItem(_username, _currentBook.Title, item);
            });

            _mediaPlayer.MediaEnded += (s, e) =>
            {
                _mediaPlayer.Position = TimeSpan.Zero;
                _mediaPlayer.Play();
            };

            _timer.Interval = TimeSpan.FromSeconds(0.5);
            _timer.Tick += (s, e) =>
            {
                if (_mediaPlayer.Source != null && _mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    _isTimerUpdating = true;
                    TotalDuration = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    CurrentPosition = _mediaPlayer.Position.TotalSeconds;
                    _isTimerUpdating = false;
                }
            };

            _ = LoadAllPagesAsync();
        }

        private void UpdateMusicPlayback()
        {
            if (!IsMusicEnabled)
            {
                if (IsMusicPlaying) IsMusicPlaying = false;
                _mediaPlayer.Stop();
                return;
            }

            if (_pageToMusicMap.Count < CurrentPageNum) return;

            string targetMusic = _pageToMusicMap[CurrentPageNum - 1];

            if (string.IsNullOrEmpty(targetMusic))
            {
                if (IsMusicPlaying) IsMusicPlaying = false;
                _mediaPlayer.Stop();
                _mediaPlayer.Close();
                _currentPlayingMusic = string.Empty;
                return;
            }

            if (_currentPlayingMusic != targetMusic)
            {
                string musicPath = "";

                if (targetMusic.StartsWith("music/") || targetMusic.StartsWith("music\\"))
                {
                    string fileName = Path.GetFileName(targetMusic);
                    musicPath = FileHelper.GetLocalFilePath(_username, _currentBook.Title, "music", fileName);
                }
                else
                {
                    musicPath = FileHelper.GetLocalFilePath(_username, _currentBook.Title, "", targetMusic);
                }

                // [수정] File.Exists 대신 _fileSystem 사용
                if (_fileSystem.FileExists(musicPath))
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Close();

                    _mediaPlayer.Open(new Uri(musicPath, UriKind.Absolute));

                    _mediaPlayer.Play();
                    _timer.Start();

                    _currentPlayingMusic = targetMusic;

                    if (!_isMusicPlaying)
                    {
                        _isMusicPlaying = true;
                        OnPropertyChanged(nameof(IsMusicPlaying));
                    }
                }
            }
            else
            {
                if (!IsMusicPlaying)
                {
                    IsMusicPlaying = true;
                }
            }
        }

        public void CheckCurrentPageStatus()
        {
            // [수정] NoteManager 직접 호출 대신 _noteService 사용
            var noteData = _noteService.LoadNotes(_username, _currentBook.Title);
            bool isSaved = noteData.Bookmarks.Any(x => x.PageNumber == CurrentPageNum);
            if (_isBookmarked != isSaved) { _isBookmarked = isSaved; OnPropertyChanged(nameof(IsBookmarked)); }
        }

        public void SaveNoteData(NoteItem item)
        {
            item.PageNumber = CurrentPageNum;
            // [수정] NoteManager 직접 호출 대신 _noteService 사용
            _noteService.AddItem(_username, _currentBook.Title, item);
        }

        private async Task LoadAllPagesAsync()
        {
            CurrentPageContent = "책을 불러오는 중...";
            _allPages.Clear();
            _pageToChapterMap.Clear();
            _chapterStartPageMap.Clear();
            TableOfContents.Clear();
            _pageToMusicMap.Clear();

            try
            {
                if (_currentBook == null || string.IsNullOrEmpty(_currentBook.FileName))
                {
                    CurrentPageContent = "오류: 책 정보가 올바르지 않습니다.";
                    return;
                }

                string? tempBaseName = Path.GetFileNameWithoutExtension(_currentBook.FileName);
                string baseName = tempBaseName ?? _currentBook.Title ?? "UnknownBook";
                string jsonFileName = baseName.EndsWith("_full") ? $"{baseName}.json" : $"{baseName}_full.json";

                string localPath = FileHelper.GetLocalFilePath(_username, _currentBook.Title, "", jsonFileName);

                // [수정] File.Exists 대신 _fileSystem 사용
                if (!_fileSystem.FileExists(localPath))
                {
                    localPath = FileHelper.GetLocalFilePath(_username, _currentBook.Title, "", _currentBook.FileName);
                }

                // [수정] File.Exists 대신 _fileSystem 사용
                if (_fileSystem.FileExists(localPath))
                {
                    // [수정] File.ReadAllTextAsync 대신 _fileSystem 사용
                    string json = await _fileSystem.ReadAllTextAsync(localPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var bookData = JsonSerializer.Deserialize<LocalBookData>(json, options);

                    if (bookData != null)
                    {
                        if (bookData.book_info != null && !string.IsNullOrEmpty(bookData.book_info.author))
                        {
                            if (_currentBook.Author != bookData.book_info.author) { _currentBook.Author = bookData.book_info.author; }
                        }

                        if (bookData.chapters != null)
                        {
                            await Task.Run(() =>
                            {
                                int globalPageCounter = 0;
                                int charLimit = 600;

                                foreach (var chapter in bookData.chapters)
                                {
                                    Application.Current.Dispatcher.Invoke(() => TableOfContents.Add(chapter.title));
                                    int chapterIdx = chapter.chapter_index;
                                    _chapterStartPageMap[chapterIdx] = globalPageCounter + 1;

                                    string titlePage = $"=== {chapter.title} ===";
                                    _allPages.Add(titlePage);
                                    _pageToChapterMap.Add(chapterIdx);

                                    string firstSegMusic = chapter.segments?.FirstOrDefault()?.music_path
                                        ?? chapter.segments?.FirstOrDefault()?.music_filename
                                        ?? string.Empty;

                                    _pageToMusicMap.Add(firstSegMusic);

                                    globalPageCounter++;

                                    StringBuilder sb = new StringBuilder();
                                    string currentMusic = firstSegMusic;

                                    if (chapter.segments != null)
                                    {
                                        foreach (var seg in chapter.segments)
                                        {
                                            currentMusic = seg.music_path ?? seg.music_filename ?? string.Empty;

                                            if (seg.pages != null)
                                            {
                                                foreach (var page in seg.pages)
                                                {
                                                    sb.Append(page.text).Append("\n\n");
                                                    if (sb.Length >= charLimit)
                                                    {
                                                        _allPages.Add(sb.ToString());
                                                        _pageToChapterMap.Add(chapterIdx);
                                                        _pageToMusicMap.Add(currentMusic);

                                                        globalPageCounter++;
                                                        sb.Clear();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (sb.Length > 0)
                                    {
                                        _allPages.Add(sb.ToString());
                                        _pageToChapterMap.Add(chapterIdx);
                                        _pageToMusicMap.Add(currentMusic);

                                        globalPageCounter++;
                                    }
                                }
                            });

                            TotalPages = _allPages.Count;

                            if (TotalPages > 0)
                            {
                                var progress = ReadingProgressManager.GetProgress(_username, _currentBook.Title);
                                if (progress != null && progress.CurrentPage > 0 && progress.CurrentPage <= TotalPages)
                                {
                                    if (progress.TotalPages != TotalPages) { CurrentPageNum = 1; }
                                    else { CurrentPageNum = progress.CurrentPage; }
                                }
                                else { CurrentPageNum = 1; }

                                if (CurrentPageNum == 1) { ReadingProgressManager.SaveProgress(_username, _currentBook.Title, 1, TotalPages); }

                                UpdateDisplayContent();
                                CheckCurrentPageStatus();

                                Application.Current.Dispatcher.Invoke(() => UpdateMusicPlayback());
                            }
                            else { CurrentPageContent = "내용이 없습니다."; }
                        }
                    }
                }
                else CurrentPageContent = "책 파일을 찾을 수 없습니다.";
            }
            catch (Exception ex) { CurrentPageContent = $"오류 발생: {ex.Message}"; }
        }

        private void UpdateDisplayContent()
        {
            if (_allPages.Count > 0 && CurrentPageNum <= _allPages.Count)
            {
                CurrentPageContent = _allPages[CurrentPageNum - 1];
                if (_pageToChapterMap.Count > CurrentPageNum - 1)
                {
                    int currentChapterIdx = _pageToChapterMap[CurrentPageNum - 1];
                    if (_selectedChapterIndex != currentChapterIdx)
                    {
                        _selectedChapterIndex = currentChapterIdx;
                        OnPropertyChanged(nameof(SelectedChapterIndex));
                    }
                }
            }
        }
    }
}