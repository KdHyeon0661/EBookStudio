using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
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
        private readonly IAudioPlaybackService _audioPlayback;
        private readonly UsageSession? _readingUsageSession;
        private readonly DispatcherTimer _usageTimer = new DispatcherTimer();
        private bool _activityReady;
        private bool _readerClosed;

        public string CurrentUser => _username;
        public int TargetPage { get; set; } = 1;

        private List<string> _allPages = new List<string>();
        private List<int> _pageToChapterMap = new List<int>();
        private Dictionary<int, int> _chapterStartPageMap = new Dictionary<int, int>();

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
                            _audioPlayback.Play();
                        }
                    }
                    else
                    {
                        _audioPlayback.Pause();
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
                        _audioPlayback.Seek(_currentPosition);
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
        public string BookFolderId => _currentBook?.FolderId ?? string.Empty;

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
                        // [수정] FolderId 사용
                        string targetId = !string.IsNullOrEmpty(_currentBook.FolderId) ? _currentBook.FolderId : _currentBook.Title;
                        ReadingProgressManager.SaveProgress(_username, targetId, _currentPageNum, TotalPages);
                        if (_activityReady)
                            _readingUsageSession?.RecordPageChange(_currentPageNum, TotalPages);
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

                    int dataKey = value + 1;

                    if (value >= 0 && _chapterStartPageMap.ContainsKey(dataKey))
                    {
                        int targetPage = _chapterStartPageMap[dataKey];
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

        public ReadBookViewModel(MainViewModel mainVM, Book book, IBookFileSystem? fileSystem = null,
                                 INoteService? noteService = null,
                                 IAudioPlaybackService? audioPlayback = null)
        {
            _mainVM = mainVM;
            _currentBook = book;
            _username = mainVM.LoggedInUser;

            _fileSystem = fileSystem ?? new BookFileSystem();
            _noteService = noteService ?? new NoteService();
            _audioPlayback = audioPlayback ?? new AudioPlaybackService();
            _readingUsageSession = UsageActivityStore.StartSession(
                _username, "reading_session", _currentBook.FolderId);
            _usageTimer.Interval = TimeSpan.FromSeconds(15);
            _usageTimer.Tick += (s, e) =>
            {
                if (Application.Current.MainWindow?.IsActive == true)
                    _readingUsageSession?.AddActiveSeconds(15, CurrentPageNum, TotalPages);
            };
            _usageTimer.Start();

            NextPageCommand = new RelayCommand(o => { if (CurrentPageNum < TotalPages) CurrentPageNum++; });
            PrevPageCommand = new RelayCommand(o => { if (CurrentPageNum > 1) CurrentPageNum--; });
            ToggleMenuCommand = new RelayCommand(o => { IsMenuVisible = !IsMenuVisible; if (!IsMenuVisible) IsTocVisible = false; });

            CloseCommand = new RelayCommand(o =>
            {
                OnReaderClosed();
                _mainVM.NavigateToHome();
            });

            ToggleMusicCommand = new RelayCommand(o => IsMusicPlaying = !IsMusicPlaying);
            ToggleTocCommand = new RelayCommand(o => IsTocVisible = !IsTocVisible);
            OpenNoteCommand = new RelayCommand(o =>
            {
                OnReaderClosed();
                _mainVM.CurrentView = new NoteViewModel(_mainVM, _currentBook, CurrentPageNum);
            });
            OpenSettingCommand = new RelayCommand(o => { });

            ToggleBookmarkCommand = new RelayCommand(o =>
            {
                IsBookmarked = !IsBookmarked;
                var item = new NoteItem { Type = "Bookmark", PageNumber = CurrentPageNum, Content = $"p.{CurrentPageNum} - {DateTime.Now:yyyy.MM.dd}", CreatedAt = DateTime.Now };

                // [수정] FolderId 사용
                if (IsBookmarked) _noteService.AddItem(_username, _currentBook.FolderId, item);
                else _noteService.RemoveItem(_username, _currentBook.FolderId, item);
            });

            _audioPlayback.ProgressChanged += (position, duration) =>
            {
                _isTimerUpdating = true;
                TotalDuration = duration;
                CurrentPosition = position;
                _isTimerUpdating = false;
            };

            _ = LoadAllPagesAsync();
        }

        public void OnReaderClosed()
        {
            if (_readerClosed) return;
            _readerClosed = true;
            _usageTimer.Stop();
            _readingUsageSession?.Complete(CurrentPageNum, TotalPages);
            _audioPlayback.Dispose();
        }

        private void UpdateMusicPlayback()
        {
            if (!IsMusicEnabled)
            {
                if (IsMusicPlaying) IsMusicPlaying = false;
                _audioPlayback.Stop();
                return;
            }

            if (_pageToMusicMap.Count < CurrentPageNum) return;

            string targetMusic = _pageToMusicMap[CurrentPageNum - 1];

            if (string.IsNullOrEmpty(targetMusic))
            {
                if (IsMusicPlaying) IsMusicPlaying = false;
                _audioPlayback.Stop();
                _audioPlayback.Close();
                _currentPlayingMusic = string.Empty;
                return;
            }

            if (_currentPlayingMusic != targetMusic)
            {
                string musicPath = "";

                if (targetMusic.StartsWith("music/") || targetMusic.StartsWith("music\\"))
                {
                    string fileName = Path.GetFileName(targetMusic);
                    // [수정] FolderId 사용
                    musicPath = FileHelper.GetLocalFilePath(_username, _currentBook.FolderId, "music", fileName);
                }
                else
                {
                    // [수정] FolderId 사용
                    musicPath = FileHelper.GetLocalFilePath(_username, _currentBook.FolderId, "", targetMusic);
                }

                if (_fileSystem.FileExists(musicPath))
                {
                    _audioPlayback.Open(musicPath);
                    _audioPlayback.Play();

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
            // [수정] FolderId 사용
            var noteData = _noteService.LoadNotes(_username, _currentBook.FolderId);
            bool isSaved = noteData.Bookmarks.Any(x => x.PageNumber == CurrentPageNum);
            if (_isBookmarked != isSaved) { _isBookmarked = isSaved; OnPropertyChanged(nameof(IsBookmarked)); }
        }

        public (IReadOnlyList<NoteItem> Highlights, IReadOnlyList<NoteItem> Memos) GetCurrentPageAnnotations()
        {
            var notes = _noteService.LoadNotes(_username, _currentBook.FolderId);
            return (
                notes.Highlights.Where(item => item.PageNumber == CurrentPageNum).ToList(),
                notes.Memos.Where(item => item.PageNumber == CurrentPageNum).ToList());
        }

        public void SaveNoteData(NoteItem item)
        {
            item.PageNumber = CurrentPageNum;
            _noteService.AddItem(_username, _currentBook.FolderId, item);
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
                // 1. 필수 정보 확인 (FolderId가 핵심)
                if (_currentBook == null || string.IsNullOrEmpty(_currentBook.FolderId))
                {
                    CurrentPageContent = "오류: 도서 고유 식별자(FolderId)가 없습니다.";
                    return;
                }

                string jsonFileName = _currentBook.FileName;

                string localPath = FileHelper.GetLocalFilePath(_username, _currentBook.FolderId, "", jsonFileName);

                if (!_fileSystem.FileExists(localPath))
                {
                    string bookDirectory = FileHelper.GetLocalFilePath(_username, _currentBook.FolderId, "", "");
                    string? discovered = Directory.Exists(bookDirectory)
                        ? Directory.GetFiles(bookDirectory, "*_full.json").FirstOrDefault()
                        : null;
                    if (!string.IsNullOrEmpty(discovered)) localPath = discovered;
                }

                if (_fileSystem.FileExists(localPath))
                {
                    string json = await _fileSystem.ReadAllTextAsync(localPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var bookData = JsonSerializer.Deserialize<LocalBookData>(json, options);

                    if (bookData != null)
                    {
                        // 작가 정보 업데이트 (기존 로직 유지)
                        if (bookData.book_info != null && !string.IsNullOrEmpty(bookData.book_info.author))
                        {
                            if (_currentBook.Author != bookData.book_info.author)
                            {
                                _currentBook.Author = bookData.book_info.author;
                            }
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

                                    // 첫 번째 세그먼트의 음악 정보 추출
                                    var firstSegment = chapter.segments?.FirstOrDefault();
                                    string firstSegMusic = !string.IsNullOrWhiteSpace(firstSegment?.music_path)
                                        ? firstSegment.music_path
                                        : firstSegment?.music_filename ?? string.Empty;

                                    _pageToMusicMap.Add(firstSegMusic);
                                    globalPageCounter++;

                                    if (chapter.segments != null)
                                    {
                                        foreach (var seg in chapter.segments)
                                        {
                                            string segmentMusic = !string.IsNullOrWhiteSpace(seg.music_path)
                                                ? seg.music_path
                                                : seg.music_filename ?? string.Empty;
                                            StringBuilder segmentTextBuilder = new StringBuilder();
                                            if (seg.pages != null)
                                            {
                                                foreach (var page in seg.pages)
                                                {
                                                    if (!string.IsNullOrWhiteSpace(page.text))
                                                        segmentTextBuilder.Append(page.text.Trim()).Append("\n\n");
                                                }
                                            }

                                            string segmentText = segmentTextBuilder.ToString().Trim();
                                            int offset = 0;
                                            while (offset < segmentText.Length)
                                            {
                                                int length = Math.Min(charLimit, segmentText.Length - offset);
                                                string readerPage = segmentText.Substring(offset, length).Trim();
                                                if (!string.IsNullOrEmpty(readerPage))
                                                {
                                                    _allPages.Add(readerPage);
                                                    _pageToChapterMap.Add(chapterIdx);
                                                    _pageToMusicMap.Add(segmentMusic);
                                                    globalPageCounter++;
                                                }
                                                offset += length;
                                            }
                                        }
                                    }
                                }
                            });

                            TotalPages = _allPages.Count;

                            if (TotalPages > 0)
                            {
                                // 4. 진도율 로드 (반드시 FolderId 사용)
                                var progress = ReadingProgressManager.GetProgress(_username, _currentBook.FolderId);

                                // 외부 이동(TargetPage) 요청이 있는지 먼저 확인
                                if (TargetPage > 1 && TargetPage <= TotalPages)
                                {
                                    CurrentPageNum = TargetPage;
                                }
                                else if (progress != null && progress.CurrentPage > 0 && progress.CurrentPage <= TotalPages)
                                {
                                    // 저장된 진도율과 전체 페이지 수가 맞는지 확인 후 이동
                                    if (progress.TotalPages != TotalPages) CurrentPageNum = 1;
                                    else CurrentPageNum = progress.CurrentPage;
                                }
                                else
                                {
                                    CurrentPageNum = 1;
                                }

                                // 첫 진입 시 진도율 초기 저장
                                if (CurrentPageNum == 1)
                                {
                                    ReadingProgressManager.SaveProgress(_username, _currentBook.FolderId, 1, TotalPages);
                                }

                                _readingUsageSession?.SetProgress(CurrentPageNum, TotalPages);
                                _activityReady = true;
                                UpdateDisplayContent();
                                CheckCurrentPageStatus();
                                Application.Current.Dispatcher.Invoke(() => UpdateMusicPlayback());
                            }
                            else { CurrentPageContent = "내용이 없습니다."; }
                        }
                    }
                }
                else
                {
                    CurrentPageContent = "책 파일을 찾을 수 없습니다.";
                }
            }
            catch (Exception ex)
            {
                CurrentPageContent = $"오류 발생: {ex.Message}";
            }
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