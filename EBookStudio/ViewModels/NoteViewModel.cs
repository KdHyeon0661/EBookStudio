using EBookStudio.Helpers;
using EBookStudio.Models;
using System;
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

        // ==========================================
        // 1. UI 상태 속성 (탭 선택)
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
        // 2. 데이터 목록 (ObservableCollection)
        // ==========================================
        public ObservableCollection<NoteItem> Bookmarks { get; } = new ObservableCollection<NoteItem>();
        public ObservableCollection<NoteItem> Highlights { get; } = new ObservableCollection<NoteItem>();
        public ObservableCollection<NoteItem> Memos { get; } = new ObservableCollection<NoteItem>();

        // ==========================================
        // 3. 개수 표시 속성 (화면 바인딩용)
        // ==========================================
        public string BookmarkCount => $"총 {Bookmarks.Count}개";
        public string HighlightCount => $"총 {Highlights.Count}개";
        public string MemoCount => $"총 {Memos.Count}개";

        // ==========================================
        // 4. 명령어 (Commands)
        // ==========================================
        public ICommand GoBackCommand { get; }      // 뒤로 가기
        public ICommand SwitchTabCommand { get; }   // 탭 변경 (라디오버튼용)
        public ICommand DeleteItemCommand { get; }  // 아이템 삭제 (휴지통)
        public ICommand GoToPageCommand { get; }    // 해당 페이지로 이동

        // ==========================================
        // 5. 생성자
        // ==========================================
        public NoteViewModel(MainViewModel mainVM, Book book, int lastPageNum)
        {
            _mainVM = mainVM;
            _currentBook = book;
            _username = mainVM.LoggedInUser;
            _lastPageNum = lastPageNum;

            // [명령어 1] 뒤로 가기 (다시 책 읽기 화면으로)
            GoBackCommand = new RelayCommand(o =>
            {
                var readerVM = new ReadBookViewModel(_mainVM, _currentBook);
                readerVM.TargetPage = _lastPageNum; // [핵심] 1페이지가 아닌, 아까 그 페이지로 설정
                _mainVM.CurrentView = readerVM;
                _mainVM.IsAuthView = true;
            });

            // [명령어 2] 탭 변경 (View의 라디오 버튼에서 "0", "1", "2"를 보냄)
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
                    // 1. 파일에서 삭제
                    NoteManager.RemoveItem(_username, _currentBook.Title, item);

                    // 2. 화면 목록 새로고침
                    LoadData();
                }
            });

            // [명령어 4] 해당 페이지로 이동
            GoToPageCommand = new RelayCommand(o =>
            {
                if (o is NoteItem item)
                {
                    var readerVM = new ReadBookViewModel(_mainVM, _currentBook);
                    readerVM.TargetPage = item.PageNumber; // [핵심] 해당 페이지로 설정
                    _mainVM.CurrentView = readerVM;
                    _mainVM.IsAuthView = true;
                }
            });

            // 초기 데이터 로드
            LoadData();
        }

        // ==========================================
        // 6. 데이터 로드 로직
        // ==========================================
        private void LoadData()
        {
            // NoteManager를 통해 JSON 파일에서 데이터 로드
            var data = NoteManager.LoadNotes(_username, _currentBook.Title);

            // 1. 책갈피 목록 갱신
            Bookmarks.Clear();
            foreach (var item in data.Bookmarks) Bookmarks.Add(item);

            // 2. 하이라이트 목록 갱신
            Highlights.Clear();
            foreach (var item in data.Highlights) Highlights.Add(item);

            // 3. 메모 목록 갱신
            Memos.Clear();
            foreach (var item in data.Memos) Memos.Add(item);

            // 4. 개수 텍스트 갱신 알림
            OnPropertyChanged(nameof(BookmarkCount));
            OnPropertyChanged(nameof(HighlightCount));
            OnPropertyChanged(nameof(MemoCount));
        }
    }
}