using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EBookStudio.ViewModels
{
    public sealed class BookDetailViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly IApiService _apiService;

        public Book Book { get; }
        public ObservableCollection<ProcessingRunItem> ProcessingRuns { get; } = new();
        public ObservableCollection<MusicTrackItem> MusicTracks { get; } = new();

        public ICommand BackCommand { get; }
        public ICommand ReadCommand { get; }
        public ICommand RefreshCommand { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRefresh));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _serverStatusMessage = string.Empty;
        public string ServerStatusMessage
        {
            get => _serverStatusMessage;
            private set
            {
                _serverStatusMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        private string _bookServerStatus = "로컬 저장됨";
        public string BookServerStatus
        {
            get => _bookServerStatus;
            private set { _bookServerStatus = value; OnPropertyChanged(); }
        }

        private long _uniqueMusicAssetCount;
        public long UniqueMusicAssetCount
        {
            get => _uniqueMusicAssetCount;
            private set
            {
                _uniqueMusicAssetCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MusicSummary));
            }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(ServerStatusMessage);
        public bool HasProcessingRuns => ProcessingRuns.Count > 0;
        public bool HasMusicTracks => MusicTracks.Count > 0;
        public bool CanRefresh => !IsLoading && _mainViewModel.IsLoggedIn &&
                                  _mainViewModel.IsNetworkAvailable;
        public string ProcessingSummary => $"처리 작업 {ProcessingRuns.Count}건";
        public string MusicSummary => $"세그먼트 {MusicTracks.Count}개 · 공용 음악 {UniqueMusicAssetCount}개";

        public BookDetailViewModel(MainViewModel mainViewModel, Book book,
                                   IApiService? apiService = null)
        {
            _mainViewModel = mainViewModel;
            _apiService = apiService ?? new ApiService();
            Book = book;

            BackCommand = new RelayCommand(_ => _mainViewModel.NavigateToHome());
            ReadCommand = new RelayCommand(_ => _mainViewModel.NavigateToReader(Book),
                _ => Book.IsAvailable);
            RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), _ => CanRefresh);
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (IsLoading) return;
            ProcessingRuns.Clear();
            MusicTracks.Clear();
            UniqueMusicAssetCount = 0;
            NotifyCollectionSummaries();

            if (!_mainViewModel.IsLoggedIn || !_mainViewModel.IsNetworkAvailable)
            {
                BookServerStatus = "오프라인";
                ServerStatusMessage = "오프라인에서도 다운로드한 책은 계속 읽을 수 있습니다.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Book.FolderId))
            {
                BookServerStatus = "서버 기록 없음";
                ServerStatusMessage = "이 책에는 서버에서 조회할 수 있는 식별자가 없습니다.";
                return;
            }

            IsLoading = true;
            ServerStatusMessage = "서버의 처리 기록과 음악 정보를 불러오는 중입니다.";
            try
            {
                Task<ApiResult<BookProcessingHistoryResponse>> historyTask =
                    _apiService.GetBookProcessingHistoryAsync(Book.FolderId);
                Task<ApiResult<BookMusicTracksResponse>> musicTask =
                    _apiService.GetBookMusicTracksAsync(Book.FolderId);
                await Task.WhenAll(historyTask, musicTask);

                ApiResult<BookProcessingHistoryResponse> history = await historyTask;
                ApiResult<BookMusicTracksResponse> music = await musicTask;
                if (history.Success && history.Value != null)
                {
                    BookServerStatus = StatusLabel(history.Value.BookStatus);
                    foreach (BookProcessingRunResponse run in history.Value.Runs)
                        ProcessingRuns.Add(ProcessingRunItem.From(run));
                }
                if (music.Success && music.Value != null)
                {
                    UniqueMusicAssetCount = music.Value.UniqueAssetCount;
                    foreach (BookMusicTrackResponse track in music.Value.Tracks)
                        MusicTracks.Add(MusicTrackItem.From(track));
                }

                NotifyCollectionSummaries();
                if (history.Success && music.Success)
                    ServerStatusMessage = "서버 정보가 최신 상태입니다.";
                else if (history.Success || music.Success)
                    ServerStatusMessage = "일부 서버 정보만 불러왔습니다. 새로고침으로 다시 시도할 수 있습니다.";
                else
                {
                    BookServerStatus = "서버 기록 없음";
                    ApiError? error = history.Error ?? music.Error;
                    ServerStatusMessage = error?.Kind == ApiErrorKind.NotFound
                        ? "서버에는 이 책의 처리 기록이 없습니다. 로컬 책은 계속 읽을 수 있습니다."
                        : error?.UserMessage ?? "서버 정보를 불러오지 못했습니다.";
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void NotifyCollectionSummaries()
        {
            OnPropertyChanged(nameof(HasProcessingRuns));
            OnPropertyChanged(nameof(HasMusicTracks));
            OnPropertyChanged(nameof(ProcessingSummary));
            OnPropertyChanged(nameof(MusicSummary));
        }

        private static string StatusLabel(string status) => status switch
        {
            "ready" => "처리 완료",
            "processing" => "처리 중",
            "failed" => "처리 실패",
            _ => string.IsNullOrWhiteSpace(status) ? "상태 미확인" : status
        };
    }

    public sealed record ProcessingRunItem(
        string ProcessType, string Status, string StatusColor, string AttemptText,
        string ModelVersion, string TimeText, string ArtifactSummary, string ErrorCode)
    {
        public static ProcessingRunItem From(BookProcessingRunResponse run)
        {
            string processType = run.ProcessType switch
            {
                "analyze" => "PDF 분석",
                "music_generation" => "AI 음악 생성",
                _ => run.ProcessType
            };
            (string status, string color) = run.Status switch
            {
                "succeeded" => ("완료", "#198754"),
                "running" => ("실행 중", "#007AFF"),
                "queued" => ("대기", "#E67E22"),
                "cancel_requested" => ("취소 처리 중", "#E67E22"),
                "cancelled" => ("취소됨", "#777777"),
                "failed" => ("실패", "#DC3545"),
                _ => (run.Status, "#777777")
            };
            string artifacts = run.Artifacts.Count == 0
                ? "산출물 없음"
                : string.Join(" · ", run.Artifacts.Select(item => ArtifactLabel(item.ArtifactType)));
            long? timestamp = run.FinishedAt ?? run.StartedAt ?? run.CreatedAt;
            return new ProcessingRunItem(processType, status, color,
                $"시도 {run.AttemptCount}/{run.MaxAttempts}",
                string.IsNullOrWhiteSpace(run.ModelVersion) ? "모델 정보 없음" : run.ModelVersion,
                FormatTime(timestamp), artifacts,
                string.IsNullOrWhiteSpace(run.ErrorCode) ? string.Empty : $"오류 코드: {run.ErrorCode}");
        }

        private static string ArtifactLabel(string type) => type switch
        {
            "source_pdf" => "원본 PDF",
            "book_json" => "책 JSON",
            "cover_image" => "표지",
            "music_index" => "음악 인덱스",
            _ => type
        };

        private static string FormatTime(long? epochSeconds)
            => epochSeconds == null ? "시간 정보 없음" : DateTimeOffset
                .FromUnixTimeSeconds(epochSeconds.Value).ToLocalTime().ToString("yyyy.MM.dd HH:mm");
    }

    public sealed record MusicTrackItem(
        string SegmentKey, string BindingType, string BindingColor,
        string GenreAndTempo, string Model, string FileName, string ReuseText)
    {
        public static MusicTrackItem From(BookMusicTrackResponse track)
        {
            (string binding, string color) = track.BindingType switch
            {
                "generated" => ("새로 생성", "#7C3AED"),
                "cache_reused" => ("공용 음악 재사용", "#007AFF"),
                "default" => ("기본 음악", "#777777"),
                _ => (track.BindingType, "#777777")
            };
            string model = string.IsNullOrWhiteSpace(track.ModelName)
                ? "모델 정보 없음"
                : $"{track.ModelName} · {track.ModelVersion}";
            string duration = track.DurationSeconds is > 0 ? $" · {track.DurationSeconds}초" : string.Empty;
            return new MusicTrackItem(track.SegmentKey, binding, color,
                $"{track.Genre} · {track.Bpm} BPM{duration}", model,
                string.IsNullOrWhiteSpace(track.FileName) ? "파일 준비 중" : track.FileName,
                $"누적 재사용 {track.ReuseCount}회");
        }
    }
}
