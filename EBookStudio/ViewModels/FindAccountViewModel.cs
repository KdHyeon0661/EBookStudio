using System.Windows.Input;
using System.Windows.Controls;
using EBookStudio.Helpers;
using EBookStudio.Services;
using EBookStudio.Models;

namespace EBookStudio.ViewModels
{
    public class FindAccountViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;

        // [수정] 직접 호출 대신 인터페이스 사용
        private readonly IAccountService _accountService;
        private readonly IDialogService _dialogService;

        // --- 입력 속성 ---
        private string _email = string.Empty;
        private string _code = string.Empty;
        private long _emailVersion;
        private long _verificationVersion;
        private bool _sendingCode;

        public string Email
        {
            get => _email;
            set
            {
                if (_email == value) return;
                _email = value;
                _emailVersion++;
                _verificationVersion++;
                IsCodeSent = false;
                IsVerified = false;
                FoundUsername = null;
                Code = string.Empty;
                OnPropertyChanged();
            }
        }

        public string Code
        {
            get => _code;
            set
            {
                if (_code == value) return;
                _code = value;
                _verificationVersion++;
                IsVerified = false;
                FoundUsername = null;
                OnPropertyChanged();
            }
        }

        // --- 상태 속성 ---
        private bool _isCodeSent;
        public bool IsCodeSent
        {
            get => _isCodeSent;
            private set { _isCodeSent = value; OnPropertyChanged(); OnPropertyChanged(nameof(SendCodeLabel)); }
        }

        public string SendCodeLabel => IsCodeSent ? "인증번호 재발송" : "인증번호 발송";

        private bool _isVerified;
        public bool IsVerified
        {
            get => _isVerified;
            private set { _isVerified = value; OnPropertyChanged(); }
        }

        private string? _foundUsername;
        public string? FoundUsername
        {
            get => _foundUsername;
            private set { _foundUsername = value; OnPropertyChanged(); }
        }

        // --- 커맨드 ---
        public ICommand SendCodeCommand { get; }
        public ICommand VerifyCodeCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand GoBackCommand { get; }

        public FindAccountViewModel(MainViewModel mainVM, IAccountService? accountService = null, IDialogService? dialogService = null)
        {
            _mainVM = mainVM;
            _accountService = accountService ?? new AccountService();
            _dialogService = dialogService ?? new DialogService();

            SendCodeCommand = new AsyncRelayCommand(async o => await ExecuteSendCode());
            VerifyCodeCommand = new AsyncRelayCommand(async o => await ExecuteVerifyCode());
            ResetPasswordCommand = new AsyncRelayCommand(ExecuteResetPassword);
            GoBackCommand = new RelayCommand(o => _mainVM.NavigateToLogin());
        }

        private async Task ExecuteSendCode()
        {
            string email = Email.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                _dialogService.ShowMessage("이메일을 입력해주세요."); // MessageBox 대체
                return;
            }

            long emailVersion = _emailVersion;
            _verificationVersion++;
            _sendingCode = true;
            CodeSendResult result;
            try { result = await _accountService.SendCodeAsync(email); }
            finally { _sendingCode = false; }
            if (emailVersion != _emailVersion) return;
            if (result.Success)
            {
                Code = string.Empty;
                IsVerified = false;
                FoundUsername = null;
                IsCodeSent = true;
                if (!string.IsNullOrWhiteSpace(result.DevelopmentCode))
                {
                    _dialogService.ShowMessage($"개발 모드 인증번호를 직접 입력해주세요.\n{result.DevelopmentCode}");
                }
                else
                {
                    _dialogService.ShowMessage("가입된 주소라면 인증번호가 이메일로 발송됩니다.");
                }
            }
            else
            {
                _dialogService.ShowMessage($"이메일 전송 실패: {result.Error?.UserMessage ?? result.Message ?? "서버 오류"}");
            }
        }

        private async Task ExecuteVerifyCode()
        {
            if (_sendingCode || !IsCodeSent) return;
            string email = Email.Trim();
            string code = Code.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                _dialogService.ShowMessage("인증번호를 입력해주세요.");
                return;
            }

            long version = _verificationVersion;
            ApiResult result = await _accountService.VerifyCodeAsync(email, code);
            if (version != _verificationVersion || _sendingCode) return;
            if (!result.Success)
            {
                _dialogService.ShowMessage($"인증 실패: {result.Error?.UserMessage ?? "인증번호를 확인해주세요."}");
                return;
            }

            ApiResult<string> findResult = await _accountService.FindIdAsync(email, code);
            if (version != _verificationVersion || _sendingCode) return;
            if (!findResult.Success)
            {
                _dialogService.ShowMessage($"아이디 조회 실패: {findResult.Error?.UserMessage ?? "요청을 처리하지 못했습니다."}");
                return;
            }

            _dialogService.ShowMessage("인증 성공!");
            IsVerified = true;
            FoundUsername = findResult.Value;
        }

        private async Task ExecuteResetPassword(object? parameter)
        {
            if (!IsVerified || _sendingCode) return;
            var passwordBox = parameter as PasswordBox;
            string? password = passwordBox?.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                _dialogService.ShowMessage("새 비밀번호를 입력해주세요.");
                return;
            }

            if (password.Length < 8)
            {
                _dialogService.ShowMessage("비밀번호는 최소 8자 이상이어야 합니다.");
                return;
            }

            long version = _verificationVersion;
            ApiResult result = await _accountService.ResetPasswordAsync(Email.Trim(), Code.Trim(), password);
            if (version != _verificationVersion) return;

            if (result.Success)
            {
                passwordBox?.Clear();
                _dialogService.ShowMessage("비밀번호가 변경되었습니다.\n로그인 화면으로 이동합니다.");
                _mainVM.NavigateToLogin();
            }
            else
            {
                _dialogService.ShowMessage($"변경 실패: {result.Error?.UserMessage ?? "요청을 처리하지 못했습니다."}");
            }
        }

        public void Clear()
        {
            _emailVersion++;
            _verificationVersion++;
            Email = string.Empty;
            Code = string.Empty;
            IsCodeSent = false;
            IsVerified = false;
            FoundUsername = null;
        }
    }
}
