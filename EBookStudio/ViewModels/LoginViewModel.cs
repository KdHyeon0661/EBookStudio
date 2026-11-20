using System.Windows.Input;
using System.Windows.Controls;
using EBookStudio.Models;
using EBookStudio.Helpers;
using EBookStudio.Services;

namespace EBookStudio.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private CancellationTokenSource? _pendingLogin;

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }
        public ICommand GoRegisterCommand { get; }
        public ICommand FindAccountCommand { get; }

        // [수정] 생성자 주입 방식 적용
        public LoginViewModel(MainViewModel mainVM,
                              IAuthService? authService = null,
                              IDialogService? dialogService = null)
        {
            _mainVM = mainVM;

            // 주입받은 것이 없으면 실제 서비스를 생성해서 사용
            _authService = authService ?? new AuthService();
            _dialogService = dialogService ?? new DialogService();

            LoginCommand = new AsyncRelayCommand(async (o) => await ExecuteLogin(o));
            GoRegisterCommand = new RelayCommand(o => _mainVM.NavigateToRegister());
            FindAccountCommand = new RelayCommand(o => _mainVM.NavigateToFindAccount());
        }

        private async Task ExecuteLogin(object? parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password;

            string requestedUsername = Username.Trim();
            if (string.IsNullOrWhiteSpace(requestedUsername) || string.IsNullOrWhiteSpace(password))
            {
                // [수정] MessageBox -> _dialogService
                _dialogService.ShowMessage("아이디와 비밀번호를 입력하세요.");
                return;
            }

            // [수정] ApiService -> _authService
            using var pending = new CancellationTokenSource();
            _pendingLogin = pending;
            ApiResult<LocalAccount> result;
            try
            {
                result = await _authService.LoginAsync(requestedUsername, password, pending.Token);
                if (pending.IsCancellationRequested) return;
            }
            catch (OperationCanceledException) when (pending.IsCancellationRequested) { return; }
            finally { _pendingLogin = null; }

            if (result.Success && result.Value is { } account)
            {
                passwordBox?.Clear();
                _mainVM.SetLoginSuccess(account);
                _dialogService.ShowMessage($"로그인 성공! 환영합니다 {account.Username}님.");
            }
            else
            {
                // [수정] MessageBox -> _dialogService
                _dialogService.ShowMessage($"로그인 실패: {result.Error?.UserMessage ?? "요청을 처리하지 못했습니다."}");
            }
        }

        public void Clear()
        {
            _pendingLogin?.Cancel();
            Username = string.Empty;
        }
    }
}
