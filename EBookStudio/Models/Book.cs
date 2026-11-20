using System;
using System.Text.Json.Serialization;
using EBookStudio.Helpers;

namespace EBookStudio.Models
{
    public class Book : ViewModelBase
    {
        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        private string _folderId = string.Empty;
        public string FolderId
        {
            get => _folderId;
            set { _folderId = value; OnPropertyChanged(); }
        }

        private bool _isAvailable = true;
        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                _isAvailable = value;
                OnPropertyChanged(nameof(IsAvailable));
                NotifyCardAction();
            }
        }

        private string _author = string.Empty;
        public string Author
        {
            get => _author;
            set { _author = value; OnPropertyChanged(); }
        }

        private string? _coverUrl = null;
        public string? CoverUrl
        {
            get => _coverUrl;
            set { _coverUrl = value; OnPropertyChanged(); }
        }

        private string _fileName = string.Empty;
        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); }
        }

        private string _jobId = string.Empty;
        public string JobId
        {
            get => _jobId;
            set
            {
                _jobId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPendingJob));
                NotifyCardAction();
            }
        }

        private string _musicJobId = string.Empty;
        public string MusicJobId
        {
            get => _musicJobId;
            set
            {
                _musicJobId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPendingJob));
                NotifyCardAction();
            }
        }

        public bool HasPendingJob => !string.IsNullOrWhiteSpace(JobId) ||
                                     !string.IsNullOrWhiteSpace(MusicJobId);

        private bool _jobControlsAvailable;
        [JsonIgnore]
        public bool JobControlsAvailable
        {
            get => _jobControlsAvailable;
            set
            {
                if (_jobControlsAvailable == value) return;
                _jobControlsAvailable = value;
                NotifyCardAction();
            }
        }

        private bool _isCancellationRequested;
        public bool IsCancellationRequested
        {
            get => _isCancellationRequested;
            set
            {
                if (_isCancellationRequested == value) return;
                _isCancellationRequested = value;
                OnPropertyChanged();
                NotifyCardAction();
            }
        }

        [JsonIgnore]
        public bool HasCancelableJob => (!IsAvailable && HasPendingJob) ||
            (JobControlsAvailable && !IsMusicDownloadPending && !string.IsNullOrWhiteSpace(MusicJobId));

        private bool _isMusicDownloadPending;
        public bool IsMusicDownloadPending
        {
            get => _isMusicDownloadPending;
            set
            {
                if (_isMusicDownloadPending == value) return;
                _isMusicDownloadPending = value;
                OnPropertyChanged();
                NotifyCardAction();
            }
        }

        [JsonIgnore]
        public string CardActionText => HasCancelableJob
            ? IsCancellationRequested ? "작업 취소 처리 중" : "작업 취소"
            : "로컬 책 삭제";

        private void NotifyCardAction()
        {
            OnPropertyChanged(nameof(HasCancelableJob));
            OnPropertyChanged(nameof(CardActionText));
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string CoverColor { get; set; } = "#FFFFFF";

        private bool _isAddButton = false;
        public bool IsAddButton
        {
            get => _isAddButton;
            set { _isAddButton = value; OnPropertyChanged(); }
        }

        private bool _isBusy = false;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        private int _lastPage = 1;
        public int LastPage
        {
            get => _lastPage;
            set
            {
                _lastPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressValue));
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        private int _totalPageCount = 1;
        public int TotalPageCount
        {
            get => _totalPageCount;
            set
            {
                _totalPageCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressValue));
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public double ProgressValue
        {
            get
            {
                if (TotalPageCount <= 0) return 0;
                double pct = (double)LastPage / TotalPageCount * 100.0;
                return Math.Min(pct, 100.0);
            }
        }

        public string ProgressText => $"{Math.Round(ProgressValue)}%";
    }
}
