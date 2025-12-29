using System;
using EBookStudio.Helpers; // [중요] ViewModelBase를 쓰기 위해 추가

namespace EBookStudio.Models
{
    // [수정 1] ViewModelBase를 상속받도록 변경
    public class Book : ViewModelBase 
    {
        // [기본 정보]
        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        private bool _isAvailable = true; // 기본값은 true (기존 책들은 클릭 돼야 하니까)
        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                _isAvailable = value;
                OnPropertyChanged(nameof(IsAvailable));
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
            set { _coverUrl = value; OnPropertyChanged(); } // 커버 이미지 바뀌면 즉시 반영
        }

        private string _fileName = string.Empty;
        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); }
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string CoverColor { get; set; } = "#FFFFFF";

        // [수정 2] UI 상태용 변수들에 OnPropertyChanged 추가 (여기가 핵심!)
        
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
                OnPropertyChanged(); // 값이 false로 바뀌면 로딩바가 사라지게 함
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set 
            { 
                _statusMessage = value; 
                OnPropertyChanged(); // "다운로드 중..." -> "완료!" 텍스트 변경 알림
            }
        }

        // [독서 진행률]
        // 진행률도 읽을 때마다 바뀌어야 하므로 알림 추가 권장
        private int _lastPage = 1;
        public int LastPage
        {
            get => _lastPage;
            set 
            { 
                _lastPage = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressValue)); // LastPage가 바뀌면 %도 다시 계산하라고 알림
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

        // 프로그레스 바 값 (0 ~ 100)
        public double ProgressValue
        {
            get
            {
                if (TotalPageCount <= 0) return 0;
                double pct = (double)LastPage / TotalPageCount * 100.0;
                return Math.Min(pct, 100.0);
            }
        }

        // 텍스트 표시용 (예: "35%")
        public string ProgressText => $"{Math.Round(ProgressValue)}%";
    }
}