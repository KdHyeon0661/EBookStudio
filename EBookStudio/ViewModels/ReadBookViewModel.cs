using EBookStudio.Helpers;
using EBookStudio.Models; // Book, ApiService 등
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EBookStudio.ViewModels
{
    public class LocalBookData
    {
        public LocalBookInfo? book_info { get; set; }
        public List<LocalChapter>? chapters { get; set; }
    }

    public class LocalBookInfo
    {
        public string title { get; set; } = string.Empty;
        public string author { get; set; } = string.Empty;
        public int total_chapters { get; set; }
    }

    public class LocalChapter
    {
        public int chapter_index { get; set; }
        public string title { get; set; } = string.Empty;
        public List<LocalSegment>? segments { get; set; }
    }

    public class LocalSegment
    {
        public int segment_index { get; set; }
        public string emotion { get; set; } = string.Empty;
        public string music_filename { get; set; } = string.Empty;
        public string music_path { get; set; } = string.Empty;
        public List<LocalPage>? pages { get; set; }
    }

    public class LocalPage
    {
        public int page_index { get; set; }
        public string text { get; set; } = string.Empty;
        public bool is_new_segment { get; set; }
    }

    public class ReadBookViewModel : ViewModelBase
    {
        // ... (필드 변수들은 기존과 동일) ...
        private readonly MainViewModel _mainVM;
        private readonly Book _currentBook;
        private readonly string _username;

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

        // ... (Slider 관련 속성: CurrentPosition, TotalDuration 등 기존과 동일) ...
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

        // ... (북마크, 메뉴, TOC 관련 속성 기존과 동일) ...
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

        // ... (ICommand 정의 및 생성자 기존과 동일) ...
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand ToggleMenuCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ToggleMusicCommand { get; }
        public ICommand ToggleTocCommand { get; }
        public ICommand OpenNoteCommand { get; }
        public ICommand OpenSettingCommand { get; }
        public ICommand ToggleBookmarkCommand { get; }

        public ReadBookViewModel(MainViewModel mainVM, Book book)
        {
            _mainVM = mainVM;
            _currentBook = book;
            _username = mainVM.LoggedInUser;

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
                if (IsBookmarked) NoteManager.AddItem(_username, _currentBook.Title, item);
                else NoteManager.RemoveItem(_username, _currentBook.Title, item);
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

        // ==============================================================
        // [핵심 수정] 음악 재생 경로 처리 (공용 폴더 대응)
        // ==============================================================
        private void UpdateMusicPlayback()
        {
            if (!IsMusicEnabled)
            {
                IsMusicPlaying = false;
                return;
            }

            if (_pageToMusicMap.Count < CurrentPageNum) return;
            string targetMusic = _pageToMusicMap[CurrentPageNum - 1]; // 예: "music/song.wav"

            if (string.IsNullOrEmpty(targetMusic))
            {
                IsMusicPlaying = false;
                _currentPlayingMusic = string.Empty;
                return;
            }

            // 곡 변경 감지
            if (_currentPlayingMusic != targetMusic)
            {
                string musicPath = "";

                // [수정 포인트] JSON 경로가 "music/" 접두사를 가지면 공용 폴더에서 찾음
                if (targetMusic.StartsWith("music/") || targetMusic.StartsWith("music\\"))
                {
                    string fileName = Path.GetFileName(targetMusic); // "song.wav" 추출
                    // "music" 카테고리를 주어 DownloadCache/music/... 경로를 얻음
                    musicPath = FileHelper.GetLocalFilePath(_username, _currentBook.Title, "music", fileName);
                }
                else
                {
                    // 예전 방식(호환성): 책 폴더 내부에 있을 경우
                    musicPath = FileHelper.GetLocalFilePath(_username, _currentBook.Title, "", targetMusic);
                }

                if (File.Exists(musicPath))
                {
                    _mediaPlayer.Open(new Uri(musicPath));
                    _currentPlayingMusic = targetMusic;
                    IsMusicPlaying = true; // 새 곡 재생
                }
                else
                {
                    // 파일이 없으면 정지 (혹은 다운로드 로직 추가 가능)
                    // System.Diagnostics.Debug.WriteLine($"음악 파일 없음: {musicPath}");
                }
            }
            else
            {
                // 같은 곡이면 재생 상태 복구
                if (!IsMusicPlaying) IsMusicPlaying = true;
            }
        }

        public void CheckCurrentPageStatus()
        {
            var noteData = NoteManager.LoadNotes(_username, _currentBook.Title);
            bool isSaved = noteData.Bookmarks.Any(x => x.PageNumber == CurrentPageNum);
            if (_isBookmarked != isSaved) { _isBookmarked = isSaved; OnPropertyChanged(nameof(IsBookmarked)); }
        }

        public void SaveNoteData(NoteItem item) { item.PageNumber = CurrentPageNum; NoteManager.AddItem(_username, _currentBook.Title, item); }

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

                // ==========================================================
                // [핵심 수정] JSON 로딩 경로 평탄화 (category: "" 사용)
                // ==========================================================
                // 기존: "texts" -> 수정: "" (책 폴더 루트에서 찾음)
                string localPath = FileHelper.GetLocalFilePath(_username, _currentBook.Title, "", jsonFileName);

                if (!File.Exists(localPath))
                {
                    localPath = FileHelper.GetLocalFilePath(_username, _currentBook.Title, "", _currentBook.FileName);
                }

                if (File.Exists(localPath))
                {
                    string json = await File.ReadAllTextAsync(localPath);
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

                                    // 챕터 첫 페이지용 음악 (우선순위: music_path -> music_filename)
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
                                            // 세그먼트별 음악 (music_path 우선)
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
                                                        _pageToMusicMap.Add(currentMusic); // 매핑

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
                                        _pageToMusicMap.Add(currentMusic); // 매핑

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