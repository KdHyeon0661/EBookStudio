using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Threading.Tasks;
using EBookStudio.Models;
using EBookStudio.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace EBookStudio.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
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

        // [수정] XAML 바인딩 이름과 일치시킴 (ExecuteRegisterCommand -> RegisterCommand)
        public ICommand RegisterCommand { get; }
        public ICommand BackCommand { get; } // GoBackCommand -> BackCommand
        public ICommand SendCodeCommand { get; }
        public ICommand VerifyCodeCommand { get; }

        public RegisterViewModel(MainViewModel mainVM,
                                 IAuthService? authService = null,
                                 IDialogService? dialogService = null)
        {
            _mainVM = mainVM;
            _authService = authService ?? new AuthService();
            _dialogService = dialogService ?? new DialogService();

            SendCodeCommand = new AsyncRelayCommand(async (o) => await ExecuteSendCode());
            VerifyCodeCommand = new AsyncRelayCommand(async (o) => await ExecuteVerifyCode());
            RegisterCommand = new AsyncRelayCommand(async (o) => await ExecuteRegister(o));
            BackCommand = new RelayCommand(o => _mainVM.NavigateToLogin());
        }

        private async Task ExecuteSendCode()
        {
            if (string.IsNullOrWhiteSpace(Email) || !Email.Contains("@"))
            {
                _dialogService.ShowMessage("유효한 이메일을 입력해주세요.");
                return;
            }

            bool success = await _authService.SendVerificationCodeAsync(Email);
            if (success)
            {
                _dialogService.ShowMessage("인증번호가 발송되었습니다.");
                IsEmailSent = true;
            }
            else
            {
                _dialogService.ShowMessage("전송 실패: 서버 오류.");
            }
        }

        private async Task ExecuteVerifyCode()
        {
            if (string.IsNullOrWhiteSpace(VerificationCode))
            {
                _dialogService.ShowMessage("인증번호를 입력해주세요.");
                return;
            }

            bool success = await _authService.VerifyCodeAsync(Email, VerificationCode);
            if (success)
            {
                _dialogService.ShowMessage("인증되었습니다.");
                IsVerified = true;
            }
            else
            {
                _dialogService.ShowMessage("인증번호가 틀렸습니다.");
            }
        }

        private async Task ExecuteRegister(object? parameter)
        {
            var values = parameter as object[];
            if (values == null || values.Length < 2) return;

            List<string> passwords = new List<string>();

            foreach (var item in values)
            {
                if (item is string s) passwords.Add(s);
                else if (item is PasswordBox pb) passwords.Add(pb.Password);
            }

            if (passwords.Count < 2)
            {
                _dialogService.ShowMessage("비밀번호 정보를 가져올 수 없습니다.");
                return;
            }

            string password = passwords[0];
            string confirmPassword = passwords[1];

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

            bool success = await _authService.RegisterAsync(Username, password, Email, VerificationCode);

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