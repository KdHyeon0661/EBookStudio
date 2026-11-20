using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using EBookStudio.Helpers;
using EBookStudio.Services;
using EBookStudio.Models;

namespace EBookStudio.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // [추가] ReadBookViewModel에 주입할 서비스 객체들
        private readonly IBookFileSystem _fileSystem;
        private readonly INoteService _noteService;
        private readonly IUsageService _usageService;
        private UsageSession? _appUsageSession;
        private string _usageUsername = string.Empty;
        private LocalAccount? _account;
        internal string LocalCacheKey => _account?.CacheKey ?? LoggedInUser;

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
            set
            {
                if (ReferenceEquals(_currentView, value)) return;
                switch (_currentView)
                {
                    case LoginViewModel login: login.Clear(); break;
                    case RegisterViewModel register: register.Clear(); break;
                    case FindAccountViewModel recovery: recovery.Clear(); break;
                }
                _currentView = value;
                OnPropertyChanged();
            }
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
        private readonly DispatcherTimer _usageTimer;

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
        public MainViewModel() : this(initializeClient: true) { }

        internal MainViewModel(bool initializeClient)
        {
            // [중요] 서비스 객체 초기화
            // 사용자님의 클래스명이 BookFileSystem, NoteService가 맞는지 꼭 확인하십시오.
            _fileSystem = new BookFileSystem();
            _noteService = new NoteService();
            _usageService = new UsageSyncService();

            _usageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _usageTimer.Tick += (s, e) =>
            {
                if (Application.Current.MainWindow?.IsActive == true)
                    _appUsageSession?.AddActiveSeconds(15);
            };
            _networkTimer = new DispatcherTimer();
            _networkTimer.Interval = TimeSpan.FromSeconds(3);
            _networkTimer.Tick += async (s, e) => await CheckNetworkStatus();
            if (initializeClient)
            {
                _usageTimer.Start();
                ApiService.SessionExpired += HandleSessionExpired;
                Application.Current.Exit += (s, e) =>
                {
                    ApiService.SessionExpired -= HandleSessionExpired;
                    FinishAppUsageSession();
                };
                _networkTimer.Start();
            }

            LibraryVM = new LibraryViewModel(this);
            LoginVM = new LoginViewModel(this);
            RegisterVM = new RegisterViewModel(this);
            FindAccountVM = new FindAccountViewModel(this);
            MyPageVM = new MyPageViewModel(string.Empty);

            MyPageVM.RequestLogout += () => Logout();
            MyPageVM.RequestLibraryImport += (item, cacheKey) => _ = LibraryVM.ImportDownloadedBook(item, cacheKey);
            MyPageVM.RequestLibraryReset += () => LibraryVM.ResetLocalLibraryAsync();
            MyPageVM.RequestReadingHistoryReset += () => _ = LibraryVM.ResetReadingProgressAsync();

            if (initializeClient && FileHelper.GetLastAccount() is { } lastAccount)
            {
                SetAccount(lastAccount);
                StartUsageForUser(LocalCacheKey);
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
            if (initializeClient) _ = CheckNetworkStatus();
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
                LibraryVM.UpdateJobControls();
                if (isConnected && IsLoggedIn && !string.IsNullOrWhiteSpace(LoggedInUser))
                {
                    RotateAppUsageSession();
                    await _usageService.SyncAsync(LocalCacheKey);
                }
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
            RotateAppUsageSession();
            CurrentView = MyPageVM;
            _ = MyPageVM.RefreshUsageAsync();
            IsAuthView = true;
        }

        public void NavigateToBookDetail(Book book)
        {
            IsTopBarVisible = true;
            CurrentView = new BookDetailViewModel(this, book);
            IsAuthView = false;
        }

        public void NavigateToReader(Book book)
        {
            IsTopBarVisible = false;
            // [수정] 사용자님의 ReadBookViewModel 생성자 파라미터 4개에 정확히 맞춤
            // 인자: MainViewModel(this), book, IBookFileSystem, INoteService
            CurrentView = new ReadBookViewModel(this, book, _fileSystem, _noteService);
            IsAuthView = true;
        }

        public void SetLoginSuccess(LocalAccount account)
        {
            FinishAppUsageSession();
            try
            {
                if (FileHelper.HasLegacyLibraryToMigrate(account) && MessageBox.Show(
                    "이 PC에 같은 이름으로 저장된 예전 보관함이 있습니다. 본인의 보관함이면 현재 계정으로 옮기시겠습니까?",
                    "기존 보관함", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                    FileHelper.MigrateLegacyAccount(account);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[Local cache migration] {error}");
                MessageBox.Show("기존 로컬 서재를 새 계정 폴더로 옮기지 못했습니다. 원래 위치에 보관되어 있습니다.");
            }
            SetAccount(account);
            IsLoggedIn = true;
            FileHelper.SaveLastAccount(account);
            StartUsageForUser(LocalCacheKey);
            _ = _usageService.SyncAsync(LocalCacheKey);
            _ = LibraryVM.LoadLibrary();
            NavigateToHome();
        }

        private void HandleSessionExpired(LocalAccount account)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_account?.CacheKey != account.CacheKey || new ApiService().ForAccount(account.CacheKey) != null) return;
                IsLoggedIn = false;
                LibraryVM.StopMonitoring();
                LibraryVM.UpdateJobControls();
                MyPageVM.SetAccount(LoggedInUser, LocalCacheKey);
            }));
        }

        public void Logout()
        {
            FinishAppUsageSession();
            LibraryVM.StopMonitoring();
            _ = new AuthService().LogoutAsync();
            FileHelper.SaveLastAccount(null);
            IsTopBarVisible = true;
            IsLoggedIn = false;
            SetAccount(null);
            _ = LibraryVM.LoadLibrary();
            NavigateToHome();
        }

        private void SetAccount(LocalAccount? account)
        {
            _account = account;
            LoggedInUser = account?.Username ?? string.Empty;
            MyPageVM.SetAccount(LoggedInUser, LocalCacheKey);
        }

        private void StartUsageForUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            if (_appUsageSession != null && string.Equals(_usageUsername, username, StringComparison.Ordinal))
                return;
            FinishAppUsageSession();
            UsageActivityStore.RecoverInterruptedSessions(username);
            _usageUsername = username;
            _appUsageSession = UsageActivityStore.StartSession(username, "app_session");
        }

        private void RotateAppUsageSession()
        {
            string username = _usageUsername;
            if (string.IsNullOrWhiteSpace(username)) return;
            FinishAppUsageSession();
            _usageUsername = username;
            _appUsageSession = UsageActivityStore.StartSession(username, "app_session");
        }

        private void FinishAppUsageSession()
        {
            _appUsageSession?.Complete();
            _appUsageSession = null;
            _usageUsername = string.Empty;
        }
    }
}
