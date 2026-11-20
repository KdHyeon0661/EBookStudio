using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.Services;
using System.Collections.ObjectModel;
using System.IO;
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
        private IAudioPlaybackService? _audioPlayback;
        private UsageSession? _readingUsageSession;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private readonly DispatcherTimer _usageTimer = new() { Interval = TimeSpan.FromSeconds(15) };
        private bool _readerClosed;

        public string CurrentUser => _username;
        private readonly int? _targetPage;

        private sealed record ReaderPage(string Text, int ChapterIndex, string MusicReference);
        private sealed record LoadedBook(string? Author, List<ReaderPage> Pages,
                                         List<string> ChapterTitles, List<int> ChapterStartPages);

        private IReadOnlyList<ReaderPage> _allPages = [];
        private IReadOnlyList<int> _chapterStartPages = [];

        public bool IsBookLoaded { get; private set; }

        private string _noteStatus = string.Empty;
        public string NoteStatus
        {
            get => _noteStatus;
            private set { _noteStatus = value; OnPropertyChanged(); }
        }

        private string _musicStatus = string.Empty;
        public string MusicStatus
        {
            get => _musicStatus;
            private set { _musicStatus = value; OnPropertyChanged(); }
        }

        private string _currentPlayingMusic = string.Empty;
        private string? _failedMusic;
        private bool _musicUpdateQueued;

        private bool _isTimerUpdating = false;

        private bool _playMusicRequested;
        private bool _isMusicPlaying;
        public bool IsMusicPlaying
        {
            get => _isMusicPlaying;
            private set
            {
                if (_isMusicPlaying != value)
                {
                    _isMusicPlaying = value;
                    OnPropertyChanged();

                }
            }
        }

        private bool _isMusicEnabled = true;
        public bool IsMusicEnabled
        {
            get => _isMusicEnabled;
            set
            {
                if (_isMusicEnabled == value) return;
                _isMusicEnabled = value;
                OnPropertyChanged();

                UpdateMusicPlayback();
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
                        RunAudio(() => _audioPlayback?.Seek(_currentPosition));
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

        public string BookTitle => _currentBook.Title;
        public string BookFolderId => _currentBook.FolderId;

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
                if (!IsBookLoaded || _readerClosed) return;
                int page = Math.Clamp(value, 1, _allPages.Count);
                if (_currentPageNum == page) return;

                _currentPageNum = page;
                _currentBook.LastPage = page;
                UpdateDisplayContent();
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageStatus));
                CheckCurrentPageStatus();
                QueueMusicUpdate();
                ReadingProgressManager.SaveProgress(_username, _currentBook.FolderId, page, TotalPages);
                _readingUsageSession?.RecordPageChange(page, TotalPages);
            }
        }

        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                _totalPages = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageStatus));
                _currentBook.TotalPageCount = _totalPages;
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

                    if (IsBookLoaded && value >= 0 && value < _chapterStartPages.Count)
                    {
                        CurrentPageNum = _chapterStartPages[value];
                        IsMenuVisible = false;
                        IsTocVisible = false;
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
                                 IAudioPlaybackService? audioPlayback = null, int? targetPage = null)
        {
            _mainVM = mainVM;
            _currentBook = book;
            _username = mainVM.LocalCacheKey;
            _targetPage = targetPage;

            _fileSystem = fileSystem ?? new BookFileSystem();
            _noteService = noteService ?? new NoteService();
            _audioPlayback = audioPlayback;
            _usageTimer.Tick += (s, e) =>
            {
                if (Application.Current?.MainWindow?.IsActive == true)
                    _readingUsageSession?.AddActiveSeconds(15, CurrentPageNum, TotalPages);
            };

            NextPageCommand = new RelayCommand(_ => CurrentPageNum++, _ => IsBookLoaded && CurrentPageNum < TotalPages);
            PrevPageCommand = new RelayCommand(_ => CurrentPageNum--, _ => IsBookLoaded && CurrentPageNum > 1);
            ToggleMenuCommand = new RelayCommand(o => { IsMenuVisible = !IsMenuVisible; if (!IsMenuVisible) IsTocVisible = false; });

            CloseCommand = new RelayCommand(o =>
            {
                OnReaderClosed();
                _mainVM.NavigateToHome();
            });

            ToggleMusicCommand = new RelayCommand(_ =>
            {
                _playMusicRequested = !IsMusicPlaying;
                _failedMusic = null;
                UpdateMusicPlayback();
            }, _ => IsBookLoaded);
            ToggleTocCommand = new RelayCommand(_ => IsTocVisible = !IsTocVisible, _ => IsBookLoaded);
            OpenNoteCommand = new RelayCommand(_ =>
            {
                OnReaderClosed();
                _mainVM.CurrentView = new NoteViewModel(_mainVM, _currentBook, CurrentPageNum, _noteService);
            }, _ => IsBookLoaded);
            OpenSettingCommand = new RelayCommand(_ => { });

            ToggleBookmarkCommand = new RelayCommand(_ =>
            {
                var item = new NoteItem
                {
                    Type = "Bookmark", Content = $"p.{CurrentPageNum} - {DateTime.Now:yyyy.MM.dd}"
                };
                if (WriteNote(item, remove: IsBookmarked)) IsBookmarked = !IsBookmarked;
            }, _ => IsBookLoaded);

            _ = LoadAllPagesAsync();
        }

        public void OnReaderClosed()
        {
            if (_readerClosed) return;
            _readerClosed = true;
            _usageTimer.Stop();
            try { _readingUsageSession?.Complete(CurrentPageNum, TotalPages); }
            finally
            {
                if (_audioPlayback is { } audio)
                {
                    audio.ProgressChanged -= OnPlaybackProgress;
                    audio.PlaybackFailed -= OnPlaybackFailed;
                    try { audio.Dispose(); }
                    catch (Exception error) when (IsAudioError(error))
                    { System.Diagnostics.Debug.WriteLine($"[Audio cleanup] {error}"); }
                }
            }
        }

        private void QueueMusicUpdate()
        {
            if (!_playMusicRequested || _musicUpdateQueued || _readerClosed) return;
            _musicUpdateQueued = true;
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _musicUpdateQueued = false;
                UpdateMusicPlayback();
            }));
        }

        private void UpdateMusicPlayback()
        {
            if (_readerClosed || !IsBookLoaded) return;
            RunAudio(() =>
            {
                if (!IsMusicEnabled || !_playMusicRequested)
                {
                    if (IsMusicPlaying) _audioPlayback?.Pause();
                    IsMusicPlaying = false;
                    return;
                }

                string target = _allPages[CurrentPageNum - 1].MusicReference;
                if (!string.Equals(_currentPlayingMusic, target, StringComparison.Ordinal))
                {
                    _audioPlayback?.Close();
                    _currentPlayingMusic = string.Empty;
                    IsMusicPlaying = false;
                }
                if (string.IsNullOrWhiteSpace(target))
                {
                    MusicStatus = string.Empty;
                    return;
                }
                if (_failedMusic == target) return;
                MusicStatus = string.Empty;

                if (_currentPlayingMusic != target)
                {
                    string? path = BookContent.FindLocalMusic(_username, _currentBook.FolderId, target, _fileSystem.FileExists);
                    if (path == null)
                    {
                        MusicStatus = "이 페이지의 음악 파일이 아직 없습니다.";
                        return;
                    }
                    if (!_audioHandlersAttached)
                    {
                        _audioPlayback ??= new AudioPlaybackService();
                        _audioPlayback.ProgressChanged += OnPlaybackProgress;
                        _audioPlayback.PlaybackFailed += OnPlaybackFailed;
                        _audioHandlersAttached = true;
                    }
                    _currentPlayingMusic = target;
                    _audioPlayback!.Open(path);
                }
                if (IsMusicPlaying) return;
                _audioPlayback!.Play();
                if (_failedMusic != target) IsMusicPlaying = true;
            });
        }

        private bool _audioHandlersAttached;

        private void OnPlaybackProgress(double position, double duration)
        {
            _isTimerUpdating = true;
            try
            {
                TotalDuration = duration;
                CurrentPosition = position;
            }
            finally { _isTimerUpdating = false; }
        }

        private void OnPlaybackFailed()
        {
            _failedMusic = IsBookLoaded ? _allPages[CurrentPageNum - 1].MusicReference : null;
            _currentPlayingMusic = string.Empty;
            IsMusicPlaying = false;
            MusicStatus = "음악을 재생할 수 없습니다.";
        }

        private void RunAudio(Action action)
        {
            try { action(); }
            catch (Exception error) when (IsAudioError(error))
            {
                System.Diagnostics.Debug.WriteLine($"[Audio] {error}");
                OnPlaybackFailed();
            }
        }

        private static bool IsAudioError(Exception error) =>
            error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
                or NotSupportedException or System.Runtime.InteropServices.ExternalException;

        public void CheckCurrentPageStatus()
        {
            if (!IsBookLoaded) return;
            try
            {
                var notes = _noteService.LoadNotes(_username, _currentBook.FolderId);
                bool isSaved = notes.Bookmarks.Any(note => note.PageNumber == CurrentPageNum);
                if (IsBookmarked != isSaved) IsBookmarked = isSaved;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                NoteStatus = "노트 파일을 읽지 못했습니다.";
            }
        }

        public (IReadOnlyList<NoteItem> Highlights, IReadOnlyList<NoteItem> Memos) GetCurrentPageAnnotations()
        {
            if (!IsBookLoaded) return ([], []);
            try
            {
                var notes = _noteService.LoadNotes(_username, _currentBook.FolderId);
                return (
                    notes.Highlights.Where(item => item.PageNumber == CurrentPageNum).ToList(),
                    notes.Memos.Where(item => item.PageNumber == CurrentPageNum).ToList());
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                NoteStatus = "노트 파일을 읽지 못했습니다.";
                return ([], []);
            }
        }

        public bool SaveNoteData(NoteItem item) => WriteNote(item, remove: false);
        public bool RemoveNoteData(NoteItem item) => WriteNote(item, remove: true);

        private bool WriteNote(NoteItem item, bool remove)
        {
            if (!IsBookLoaded || _readerClosed) return false;
            try
            {
                item.PageNumber = CurrentPageNum;
                if (remove) _noteService.RemoveItem(_username, _currentBook.FolderId, item);
                else _noteService.AddItem(_username, _currentBook.FolderId, item);
                NoteStatus = string.Empty;
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                NoteStatus = "노트 변경을 저장하지 못했습니다.";
                MessageBox.Show(NoteStatus, "EBookStudio");
                return false;
            }
        }

        private async Task LoadAllPagesAsync()
        {
            CurrentPageContent = "책을 불러오는 중...";
            try
            {
                if (string.IsNullOrWhiteSpace(_currentBook.FolderId))
                    throw new IOException("책 폴더 정보가 없습니다.");

                string localPath = FileHelper.GetLocalFilePath(_username, _currentBook.FolderId, "", _currentBook.FileName);
                if (!_fileSystem.FileExists(localPath))
                {
                    string folder = FileHelper.GetBookDirectory(_username, _currentBook.FolderId);
                    localPath = Directory.Exists(folder)
                        ? Directory.EnumerateFiles(folder, "*_full.json").FirstOrDefault() ?? localPath : localPath;
                }
                if (!_fileSystem.FileExists(localPath))
                    throw new FileNotFoundException("책 파일을 찾을 수 없습니다. 서버 보관함에서 다시 받아주세요.");

                string json = await _fileSystem.ReadAllTextAsync(localPath);
                if (_readerClosed) return;
                LoadedBook loaded = await Task.Run(() => BuildPages(json));
                if (_readerClosed) return;

                _allPages = loaded.Pages;
                _chapterStartPages = loaded.ChapterStartPages;
                TotalPages = _allPages.Count;
                if (!string.IsNullOrWhiteSpace(loaded.Author)) _currentBook.Author = loaded.Author;
                var progress = ReadingProgressManager.GetProgress(_username, _currentBook.FolderId);
                if (_targetPage is int target && target >= 1 && target <= TotalPages)
                    _currentPageNum = target;
                else if (progress?.CurrentPage > 0)
                    _currentPageNum = progress.TotalPages == TotalPages
                        ? Math.Clamp(progress.CurrentPage, 1, TotalPages)
                        : ReaderIndexMapper.MapProgressPage(progress.CurrentPage, progress.TotalPages, TotalPages);

                _currentBook.LastPage = _currentPageNum;
                foreach (string title in loaded.ChapterTitles) TableOfContents.Add(title);
                IsBookLoaded = true;
                UpdateDisplayContent();
                OnPropertyChanged(nameof(CurrentPageNum));
                OnPropertyChanged(nameof(PageStatus));
                OnPropertyChanged(nameof(IsBookLoaded));
                CommandManager.InvalidateRequerySuggested();

                _ = _dispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() => StartOptionalFeatures(progress)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                if (!_readerClosed) CurrentPageContent = $"오류: {error.Message}";
            }
        }

        private static LoadedBook BuildPages(string json)
        {
            LocalBookData book = BookContent.Parse(json);
            var loaded = new LoadedBook(book.book_info?.author, [], [], []);
            foreach (LocalChapter chapter in book.chapters!)
            {
                int chapterIndex = loaded.ChapterTitles.Count;
                loaded.ChapterStartPages.Add(loaded.Pages.Count + 1);
                loaded.ChapterTitles.Add(chapter.title);
                loaded.Pages.Add(new ReaderPage($"=== {chapter.title} ===", chapterIndex,
                    chapter.segments?.FirstOrDefault()?.GetMusicReference() ?? string.Empty));
                foreach (LocalSegment segment in chapter.segments ?? [])
                {
                    if (segment == null) continue;
                    string music = segment.GetMusicReference();
                    foreach (LocalPage page in segment.pages ?? [])
                    {
                        string? body = page?.text?.Trim();
                        if (!string.IsNullOrWhiteSpace(body)) loaded.Pages.Add(new ReaderPage(body, chapterIndex, music));
                    }
                }
            }
            return loaded;
        }

        private void StartOptionalFeatures(ReadingProgress? progress)
        {
            if (_readerClosed) return;
            bool canUpdateProgress = true;
            try { MigrateSavedNotes(progress?.TotalPages ?? TotalPages); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                NoteStatus = "노트 파일을 읽거나 갱신하지 못했습니다.";
                canUpdateProgress = progress == null || progress.TotalPages == TotalPages;
            }

            if (canUpdateProgress && (progress == null || progress.TotalPages != TotalPages ||
                progress.CurrentPage != CurrentPageNum))
                ReadingProgressManager.SaveProgress(_username, _currentBook.FolderId, CurrentPageNum, TotalPages);

            CheckCurrentPageStatus();
            _readingUsageSession = UsageActivityStore.StartSession(_username, "reading_session", _currentBook.FolderId);
            _readingUsageSession?.SetProgress(CurrentPageNum, TotalPages);
            if (_readingUsageSession != null) _usageTimer.Start();
        }

        private void MigrateSavedNotes(int previousTotalPages)
        {
            var notes = _noteService.LoadNotes(_username, _currentBook.FolderId);
            bool changed = ReaderNoteMigrator.MigrateTextLocations(
                _allPages.Select(page => page.Text).ToList(), notes.Highlights, notes.Memos);

            if (previousTotalPages > 0 && previousTotalPages != TotalPages)
            {
                foreach (NoteItem bookmark in notes.Bookmarks)
                {
                    int mapped = ReaderIndexMapper.MapProgressPage(
                        bookmark.PageNumber, previousTotalPages, TotalPages);
                    if (bookmark.PageNumber != mapped)
                    {
                        bookmark.PageNumber = mapped;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                _noteService.SaveNotes(_username, _currentBook.FolderId, new BookNoteData
                {
                    BookTitle = _currentBook.Title,
                    Bookmarks = notes.Bookmarks,
                    Highlights = notes.Highlights,
                    Memos = notes.Memos
                });
            }
        }

        private void UpdateDisplayContent()
        {
            ReaderPage page = _allPages[CurrentPageNum - 1];
            CurrentPageContent = page.Text;
            if (_selectedChapterIndex != page.ChapterIndex)
            {
                _selectedChapterIndex = page.ChapterIndex;
                OnPropertyChanged(nameof(SelectedChapterIndex));
            }
        }
    }
}
