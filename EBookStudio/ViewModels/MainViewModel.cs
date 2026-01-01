using System;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using EBookStudio.Helpers;
using EBookStudio.Models;

namespace EBookStudio.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // [추가] ReadBookViewModel에 주입할 서비스 객체들
        private readonly IBookFileSystem _fileSystem;
        private readonly INoteService _noteService;

        // ==========================================
        // 1. 상태 관리 변수
        // ==========================================
        private bool _isTopBarVisible = true;
        public bool IsTopBarVisible
        {
            get => _isTopBarVisible;
            set { _isTopBarVisible = value; OnPropertyChanged(); }
        }

        private bool _isNetworkAvailable = false;
        public bool IsNetworkAvailable
        {
            get => _isNetworkAvailable;
            set
            {
                if (_isNetworkAvailable != value)
                {
                    _isNetworkAvailable = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool _isLoggedIn = false;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                _isLoggedIn = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _loggedInUser = string.Empty;
        public string LoggedInUser
        {
            get => _loggedInUser;
            set { _loggedInUser = value; OnPropertyChanged(); }
        }

        private bool _isAuthView = false;
        public bool IsAuthView
        {
            get => _isAuthView;
            set { _isAuthView = value; OnPropertyChanged(); }
        }

        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        // ==========================================
        // 2. 하위 뷰모델 및 타이머
        // ==========================================
        public LibraryViewModel LibraryVM { get; set; }
        public LoginViewModel LoginVM { get; set; }
        public RegisterViewModel RegisterVM { get; set; }
        public FindAccountViewModel FindAccountVM { get; set; }
        public MyPageViewModel MyPageVM { get; set; }

        private DispatcherTimer _networkTimer;

        // ==========================================
        // 3. 명령어 (Commands)
        // ==========================================
        public ICommand GoHomeCommand { get; }
        public ICommand GoLoginCommand { get; }
        public ICommand GoRegisterCommand { get; }
        public ICommand GoMyPageCommand { get; }
        public ICommand LogoutCommand { get; }

        // ==========================================
        // 4. 생성자 (초기화)
        // ==========================================
        public MainViewModel()
        {
            // [중요] 서비스 객체 초기화
            // 사용자님의 클래스명이 BookFileSystem, NoteService가 맞는지 꼭 확인하십시오.
            _fileSystem = new BookFileSystem();
            _noteService = new NoteService();

            _networkTimer = new DispatcherTimer();
            _networkTimer.Interval = TimeSpan.FromSeconds(3);
            _networkTimer.Tick += async (s, e) => await CheckNetworkStatus();
            _networkTimer.Start();

            _ = CheckNetworkStatus();

            LibraryVM = new LibraryViewModel(this);
            LoginVM = new LoginViewModel(this);
            RegisterVM = new RegisterViewModel(this);
            FindAccountVM = new FindAccountViewModel(this);
            MyPageVM = new MyPageViewModel(string.Empty);

            MyPageVM.RequestLogout += () => Logout();

            string? lastUser = FileHelper.GetLastUser();
            if (!string.IsNullOrEmpty(lastUser))
            {
                LoggedInUser = lastUser;
                MyPageVM.Username = lastUser;
                _ = LibraryVM.LoadLibrary();
            }

            NavigateToHome();

            GoHomeCommand = new RelayCommand(o => NavigateToHome());

            GoMyPageCommand = new RelayCommand(
                execute: o => NavigateToMyPage(),
                canExecute: o => IsLoggedIn || !string.IsNullOrEmpty(LoggedInUser)
            );

            GoLoginCommand = new RelayCommand(
                execute: o => NavigateToLogin(),
                canExecute: o => IsNetworkAvailable && !IsLoggedIn
            );

            GoRegisterCommand = new RelayCommand(
                execute: o => NavigateToRegister(),
                canExecute: o => IsNetworkAvailable && !IsLoggedIn
            );

            LogoutCommand = new RelayCommand(o => Logout());
        }

        // ==========================================
        // 5. 핵심 메서드
        // ==========================================
        private async Task CheckNetworkStatus()
        {
            bool isConnected = await NetworkHelper.CheckInternetConnectionAsync();
            if (IsNetworkAvailable != isConnected)
            {
                IsNetworkAvailable = isConnected;
            }
        }

        public void NavigateToHome()
        {
            IsTopBarVisible = true;
            CurrentView = LibraryVM;
            IsAuthView = false;
        }

        public void NavigateToLogin()
        {
            IsTopBarVisible = true;
            LoginVM.Clear();
            CurrentView = LoginVM;
            IsAuthView = true;
        }

        public void NavigateToRegister()
        {
            IsTopBarVisible = true;
            RegisterVM.Clear();
            CurrentView = RegisterVM;
            IsAuthView = true;
        }

        public void NavigateToFindAccount()
        {
            IsTopBarVisible = true;
            FindAccountVM.Clear();
            CurrentView = FindAccountVM;
            IsAuthView = true;
        }

        public void NavigateToMyPage()
        {
            IsTopBarVisible = true;
            MyPageVM.Username = LoggedInUser;
            CurrentView = MyPageVM;
            IsAuthView = true;
        }

        public void NavigateToReader(Book book)
        {
            IsTopBarVisible = false;
            // [수정] 사용자님의 ReadBookViewModel 생성자 파라미터 4개에 정확히 맞춤
            // 인자: MainViewModel(this), book, IBookFileSystem, INoteService
            CurrentView = new ReadBookViewModel(this, book, _fileSystem, _noteService);
            IsAuthView = true;
        }

        public void SetLoginSuccess(string username)
        {
            IsLoggedIn = true;
            LoggedInUser = username;
            MyPageVM.Username = username;
            FileHelper.SaveLastUser(username);
            _ = LibraryVM.LoadLibrary();
            NavigateToHome();
        }

        public void Logout()
        {
            ApiService.Logout();
            IsTopBarVisible = true;
            IsLoggedIn = false;
            LoggedInUser = string.Empty;
            MyPageVM.Username = string.Empty;
            _ = LibraryVM.LoadLibrary();
            NavigateToHome();
        }
    }
}