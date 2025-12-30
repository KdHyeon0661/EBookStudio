using System;
using System.Windows.Input;
using System.Windows.Threading; // 타이머 사용
using System.Threading.Tasks;
using EBookStudio.Helpers;
using EBookStudio.Models;

namespace EBookStudio.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
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
            // 1. 네트워크 상태 체크 타이머 설정
            _networkTimer = new DispatcherTimer();
            _networkTimer.Interval = TimeSpan.FromSeconds(3);
            _networkTimer.Tick += async (s, e) => await CheckNetworkStatus();
            _networkTimer.Start();

            _ = CheckNetworkStatus();

            // 2. 하위 뷰모델 생성
            LibraryVM = new LibraryViewModel(this);
            LoginVM = new LoginViewModel(this);
            RegisterVM = new RegisterViewModel(this);
            FindAccountVM = new FindAccountViewModel(this);

            // [수정 완료] 생성자에 빈 문자열 전달 (MyPageViewModel(string username)에 맞춤)
            MyPageVM = new MyPageViewModel(string.Empty);

            // 로그아웃 요청 시 실행될 이벤트 연결
            MyPageVM.RequestLogout += () => Logout();

            // 3. 마지막 사용자 확인 및 자동 동기화
            string? lastUser = FileHelper.GetLastUser();

            if (!string.IsNullOrEmpty(lastUser))
            {
                // 정보 복구
                LoggedInUser = lastUser;
                MyPageVM.Username = lastUser; // [중요] 저장된 유저명으로 업데이트

                // 서재 로드
                _ = LibraryVM.LoadLibrary();
            }

            // 4. 홈(서재)으로 시작
            NavigateToHome();

            // 5. 명령어 연결
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
            // 마이페이지 이동 시 현재 유저명 다시 갱신
            MyPageVM.Username = LoggedInUser;
            CurrentView = MyPageVM;
            IsAuthView = true;
        }

        public void NavigateToReader(Book book)
        {
            // 책 읽을 때는 상단 바 숨김
            IsTopBarVisible = false;
            CurrentView = new ReadBookViewModel(this, book);
            IsAuthView = true;
        }

        public void SetLoginSuccess(string username)
        {
            IsLoggedIn = true;
            LoggedInUser = username;

            // [중요] 로그인 성공 시 마이페이지 ViewModel에도 유저 정보 전달
            MyPageVM.Username = username;

            FileHelper.SaveLastUser(username);

            _ = LibraryVM.LoadLibrary();

            NavigateToHome();
        }

        public void Logout()
        {
            // ApiService 토큰 삭제
            ApiService.Logout();

            IsTopBarVisible = true;
            IsLoggedIn = false;
            LoggedInUser = string.Empty;

            // 마이페이지 정보도 초기화
            MyPageVM.Username = string.Empty;

            _ = LibraryVM.LoadLibrary();
            NavigateToHome();
        }
    }
}