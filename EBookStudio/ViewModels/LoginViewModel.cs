using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using EBookStudio.Models;
using EBookStudio.Helpers;
using System.Threading.Tasks;

namespace EBookStudio.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;

        // [추가] 의존성 주입을 위한 서비스 필드
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;

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

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                // [수정] MessageBox -> _dialogService
                _dialogService.ShowMessage("아이디와 비밀번호를 입력하세요.");
                return;
            }

            // [수정] ApiService -> _authService
            bool success = await _authService.LoginAsync(Username, password);

            if (success)
            {
                // [수정] MessageBox -> _dialogService
                _dialogService.ShowMessage($"로그인 성공! 환영합니다 {Username}님.");
                _mainVM.SetLoginSuccess(Username);
            }
            else
            {
                // [수정] MessageBox -> _dialogService
                _dialogService.ShowMessage("로그인 실패: 아이디 또는 비밀번호가 틀립니다.");
            }
        }

        public void Clear()
        {
            Username = string.Empty;
        }
    }
}