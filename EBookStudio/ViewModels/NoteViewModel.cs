using EBookStudio.Helpers;
using EBookStudio.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace EBookStudio.ViewModels
{
    public class NoteViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private readonly Book _currentBook;
        private readonly string _username;
        private readonly int _lastPageNum;

        // [추가] 외부에서 주입받을 서비스 필드
        private readonly INoteService _noteService;

        // ==========================================
        // 1. UI 상태 속성
        // ==========================================
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

        // ==========================================
        // 2. 데이터 목록
        // ==========================================
        public ObservableCollection<NoteItem> Bookmarks { get; } = new ObservableCollection<NoteItem>();
        public ObservableCollection<NoteItem> Highlights { get; } = new ObservableCollection<NoteItem>();
        public ObservableCollection<NoteItem> Memos { get; } = new ObservableCollection<NoteItem>();

        // ==========================================
        // 3. 개수 표시 속성
        // ==========================================
        public string BookmarkCount => $"총 {Bookmarks.Count}개";
        public string HighlightCount => $"총 {Highlights.Count}개";
        public string MemoCount => $"총 {Memos.Count}개";

        // ==========================================
        // 4. 명령어 (Commands)
        // ==========================================
        public ICommand GoBackCommand { get; }
        public ICommand SwitchTabCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand GoToPageCommand { get; }

        // ==========================================
        // 5. 생성자 (DI 적용)
        // ==========================================
        public NoteViewModel(MainViewModel mainVM, Book book, int lastPageNum, INoteService? noteService = null)
        {
            _mainVM = mainVM;
            _currentBook = book;
            _username = mainVM.LoggedInUser;
            _lastPageNum = lastPageNum;

            // 주입된 서비스가 없으면 실제 서비스를 사용함 (테스트 시에는 가짜를 넣어줌)
            _noteService = noteService ?? new NoteService();

            // [명령어 1] 뒤로 가기
            GoBackCommand = new RelayCommand(o =>
            {
                var readerVM = new ReadBookViewModel(_mainVM, _currentBook);
                readerVM.TargetPage = _lastPageNum;
                _mainVM.CurrentView = readerVM;
                _mainVM.IsAuthView = true;
            });

            // [명령어 2] 탭 변경
            SwitchTabCommand = new RelayCommand(o =>
            {
                if (int.TryParse(o?.ToString(), out int index))
                {
                    SelectedTabIndex = index;
                }
            });

            // [명령어 3] 아이템 삭제
            DeleteItemCommand = new RelayCommand(o =>
            {
                if (o is NoteItem item)
                {
                    // 서비스 인터페이스를 통해 삭제 처리
                    _noteService.RemoveItem(_username, _currentBook.Title, item);
                    LoadData();
                }
            });

            // [명령어 4] 해당 페이지로 이동
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

        // ==========================================
        // 6. 데이터 로드 로직
        // ==========================================
        private void LoadData()
        {
            // 인터페이스를 통해 데이터 로드
            var data = _noteService.LoadNotes(_username, _currentBook.Title);

            Bookmarks.Clear();
            if (data.Bookmarks != null)
                foreach (var item in data.Bookmarks) Bookmarks.Add(item);

            Highlights.Clear();
            if (data.Highlights != null)
                foreach (var item in data.Highlights) Highlights.Add(item);

            Memos.Clear();
            if (data.Memos != null)
                foreach (var item in data.Memos) Memos.Add(item);

            // 개수 문자열 갱신 알림
            OnPropertyChanged(nameof(BookmarkCount));
            OnPropertyChanged(nameof(HighlightCount));
            OnPropertyChanged(nameof(MemoCount));
        }
    }
}