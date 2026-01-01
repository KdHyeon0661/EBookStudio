using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Threading.Tasks;
using EBookStudio.Models;
using EBookStudio.Helpers;

namespace EBookStudio.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        // [추가] 인터페이스 필드
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;

        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _verificationCode = string.Empty;

        private bool _isEmailSent = false;
        private bool _isVerified = false;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string VerificationCode
        {
            get => _verificationCode;
            set { _verificationCode = value; OnPropertyChanged(); }
        }

        public bool IsEmailSent
        {
            get => _isEmailSent;
            set { _isEmailSent = value; OnPropertyChanged(); }
        }

        public bool IsVerified
        {
            get => _isVerified;
            set { _isVerified = value; OnPropertyChanged(); }
        }

        public ICommand RegisterCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand SendCodeCommand { get; }
        public ICommand VerifyCodeCommand { get; }

        // [수정] 생성자 주입 방식 (파라미터 추가)
        public RegisterViewModel(MainViewModel mainVM,
                                 IAuthService? authService = null,
                                 IDialogService? dialogService = null)
        {
            _mainVM = mainVM;
            // 서비스 초기화
            _authService = authService ?? new AuthService();
            _dialogService = dialogService ?? new DialogService();

            BackCommand = new RelayCommand(o => _mainVM.NavigateToLogin());
            RegisterCommand = new AsyncRelayCommand(async (o) => await ExecuteRegister(o), CanRegister);
            SendCodeCommand = new AsyncRelayCommand(async (o) => await ExecuteSendCode());
            VerifyCodeCommand = new AsyncRelayCommand(async (o) => await ExecuteVerifyCode());
        }

        private async Task ExecuteSendCode()
        {
            if (string.IsNullOrWhiteSpace(Email) || !Email.Contains("@"))
            {
                _dialogService.ShowMessage("유효한 이메일 주소를 입력해주세요.");
                return;
            }

            // [수정] _authService 사용
            bool success = await _authService.SendVerificationCodeAsync(Email);

            if (success)
            {
                IsEmailSent = true;
                _dialogService.ShowMessage("인증 코드가 이메일로 발송되었습니다. (가상)");
            }
            else
            {
                _dialogService.ShowMessage("인증 코드 발송에 실패했습니다. 이메일을 확인하거나 잠시 후 다시 시도해주세요.");
            }
        }

        private async Task ExecuteVerifyCode()
        {
            if (string.IsNullOrWhiteSpace(VerificationCode))
            {
                _dialogService.ShowMessage("인증 코드를 입력해주세요.");
                return;
            }

            // [수정] _authService 사용
            bool success = await _authService.VerifyCodeAsync(Email, VerificationCode);

            if (success)
            {
                IsVerified = true;
                _dialogService.ShowMessage("이메일 인증이 완료되었습니다!");
                CommandManager.InvalidateRequerySuggested();
            }
            else
            {
                _dialogService.ShowMessage("인증 코드가 일치하지 않거나 만료되었습니다.");
                IsVerified = false;
            }
        }

        private bool CanRegister(object? parameter)
        {
            return IsVerified;
        }

        private async Task ExecuteRegister(object? parameter)
        {
            var passwords = parameter as object[];

            if (passwords == null || passwords.Length < 2)
            {
                _dialogService.ShowMessage("비밀번호 입력 값을 불러올 수 없습니다.");
                return;
            }

            string? password = passwords[0] as string;
            string? confirmPassword = passwords[1] as string;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                _dialogService.ShowMessage("모든 항목을 입력해주세요.");
                return;
            }

            if (password != confirmPassword)
            {
                _dialogService.ShowMessage("비밀번호와 비밀번호 확인이 일치하지 않습니다.");
                return;
            }

            if (!IsVerified)
            {
                _dialogService.ShowMessage("이메일 인증을 완료해주세요.");
                return;
            }

            // [수정] _authService 사용
            bool success = await _authService.RegisterAsync(Username, password, Email);

            if (success)
            {
                _dialogService.ShowMessage("가입 성공! 로그인 화면으로 이동합니다.");
                _mainVM.NavigateToLogin();
            }
            else
            {
                _dialogService.ShowMessage("가입 실패: 이미 존재하는 아이디거나 서버 오류입니다.");
            }
        }

        public void Clear()
        {
            Username = string.Empty;
            Email = string.Empty;
            VerificationCode = string.Empty;
            IsEmailSent = false;
            IsVerified = false;
        }
    }
}