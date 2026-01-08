using System;
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