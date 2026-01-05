using System.Threading.Tasks;
using System.Windows.Input;
using EBookStudio.Helpers; // 인터페이스 포함
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
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;

        // --- 상태 속성 ---
        private bool _isCodeSent;
        public bool IsCodeSent
        {
            get => _isCodeSent;
            set { _isCodeSent = value; OnPropertyChanged(); }
        }

        private bool _isVerified;
        public bool IsVerified
        {
            get => _isVerified;
            set { _isVerified = value; OnPropertyChanged(); }
        }

        private string? _foundUsername;
        public string? FoundUsername
        {
            get => _foundUsername;
            set { _foundUsername = value; OnPropertyChanged(); }
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
            ResetPasswordCommand = new AsyncRelayCommand(async o => await ExecuteResetPassword());
            GoBackCommand = new RelayCommand(o => _mainVM.NavigateToLogin());
        }

        private async Task ExecuteSendCode()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                _dialogService.ShowMessage("이메일을 입력해주세요."); // MessageBox 대체
                return;
            }

            bool success = await _accountService.SendCodeAsync(Email); // ApiService 대체
            if (success)
            {
                _dialogService.ShowMessage("인증번호가 발송되었습니다.");
                IsCodeSent = true;
            }
            else
            {
                _dialogService.ShowMessage("이메일 전송 실패: 서버 오류거나 가입되지 않은 이메일입니다.");
            }
        }

        private async Task ExecuteVerifyCode()
        {
            if (string.IsNullOrWhiteSpace(Code))
            {
                _dialogService.ShowMessage("인증번호를 입력해주세요.");
                return;
            }

            bool success = await _accountService.VerifyCodeAsync(Email, Code);
            if (success)
            {
                _dialogService.ShowMessage("인증 성공!");
                IsVerified = true;
                FoundUsername = await _accountService.FindIdAsync(Email);
            }
            else
            {
                _dialogService.ShowMessage("인증번호가 틀렸습니다.");
            }
        }

        private async Task ExecuteResetPassword()
        {
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                _dialogService.ShowMessage("새 비밀번호를 입력해주세요.");
                return;
            }

            if (NewPassword.Length < 8)
            {
                _dialogService.ShowMessage("비밀번호는 최소 8자 이상이어야 합니다.");
                return;
            }

            bool success = await _accountService.ResetPasswordAsync(Email, Code, NewPassword);

            if (success)
            {
                _dialogService.ShowMessage("비밀번호가 변경되었습니다.\n로그인 화면으로 이동합니다.");
                _mainVM.NavigateToLogin();
            }
            else
            {
                _dialogService.ShowMessage("변경 실패: 인증 시간이 만료되었거나 오류가 발생했습니다.");
            }
        }

        public void Clear()
        {
            Email = string.Empty;
            Code = string.Empty;
            NewPassword = string.Empty;
            IsCodeSent = false;
            IsVerified = false;
            FoundUsername = null;
        }
    }
}