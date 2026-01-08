using EBookStudio.Helpers;
using EBookStudio.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace EBookStudio.ViewModels
{
    public class NoteViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private readonly Book _currentBook;
        private readonly string _username;
        private readonly int _lastPageNum;

        private readonly INoteService _noteService;

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<NoteItem> Bookmarks { get; } = new ObservableCollection<NoteItem>();
        public ObservableCollection<NoteItem> Highlights { get; } = new ObservableCollection<NoteItem>();
        public ObservableCollection<NoteItem> Memos { get; } = new ObservableCollection<NoteItem>();

        public string BookmarkCount => $"총 {Bookmarks.Count}개";
        public string HighlightCount => $"총 {Highlights.Count}개";
        public string MemoCount => $"총 {Memos.Count}개";

        public ICommand GoBackCommand { get; }
        public ICommand SwitchTabCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand GoToPageCommand { get; }

        public NoteViewModel(MainViewModel mainVM, Book book, int lastPageNum, INoteService? noteService = null)
        {
            _mainVM = mainVM;
            _currentBook = book;
            _username = mainVM.LoggedInUser;
            _lastPageNum = lastPageNum;

            if (string.IsNullOrEmpty(_currentBook.FolderId)) _currentBook.FolderId = _currentBook.Title;

            _noteService = noteService ?? new NoteService();

            GoBackCommand = new RelayCommand(o =>
            {
                var readerVM = new ReadBookViewModel(_mainVM, _currentBook);
                readerVM.TargetPage = _lastPageNum;
                _mainVM.CurrentView = readerVM;
                _mainVM.IsAuthView = true;
            });

            SwitchTabCommand = new RelayCommand(o =>
            {
                if (int.TryParse(o?.ToString(), out int index))
                {
                    SelectedTabIndex = index;
                }
            });

            DeleteItemCommand = new RelayCommand(o =>
            {
                if (o is NoteItem item)
                {
                    // [수정] FolderId 사용
                    _noteService.RemoveItem(_username, _currentBook.FolderId, item);
                    LoadData();
                }
            });

            GoToPageCommand = new RelayCommand(o =>
            {
                if (o is NoteItem item)
                {
                    var readerVM = new ReadBookViewModel(_mainVM, _currentBook);
                    readerVM.TargetPage = item.PageNumber;
                    _mainVM.CurrentView = readerVM;
                    _mainVM.IsAuthView = true;
                }
            });

            LoadData();
        }

        private void LoadData()
        {
            // [수정] FolderId 사용
            var data = _noteService.LoadNotes(_username, _currentBook.FolderId);

            Bookmarks.Clear();
            if (data.Bookmarks != null)
                foreach (var item in data.Bookmarks) Bookmarks.Add(item);

            Highlights.Clear();
            if (data.Highlights != null)
                foreach (var item in data.Highlights) Highlights.Add(item);

            Memos.Clear();
            if (data.Memos != null)
                foreach (var item in data.Memos) Memos.Add(item);

            OnPropertyChanged(nameof(BookmarkCount));
            OnPropertyChanged(nameof(HighlightCount));
            OnPropertyChanged(nameof(MemoCount));
        }
    }
}