using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EBookStudio.Helpers; // AsyncRelayCommand가 여기 있음
using EBookStudio.Models;

namespace EBookStudio.ViewModels
{
    public class FindAccountViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;

        // --- 입력 속성 ---
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;

        // --- 상태 속성 (UI 제어) ---
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

        // =========================================================
        // [생성자] 커맨드 연결
        // =========================================================
        public FindAccountViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;

            // 1. 인증번호 발송
            SendCodeCommand = new AsyncRelayCommand(async o => await ExecuteSendCode());

            // 2. 인증번호 검증
            VerifyCodeCommand = new AsyncRelayCommand(async o => await ExecuteVerifyCode());

            // 3. [핵심] 비밀번호 재설정 (여기서 아래 메서드를 호출합니다)
            ResetPasswordCommand = new AsyncRelayCommand(async o => await ExecuteResetPassword());

            // 뒤로가기
            GoBackCommand = new RelayCommand(o => _mainVM.NavigateToLogin());
        }

        // =========================================================
        // [메서드] 실제 로직
        // =========================================================

        private async Task ExecuteSendCode()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                MessageBox.Show("이메일을 입력해주세요.");
                return;
            }

            bool success = await ApiService.SendCodeAsync(Email);
            if (success)
            {
                MessageBox.Show("인증번호가 발송되었습니다.");
                IsCodeSent = true;
            }
            else
            {
                MessageBox.Show("이메일 전송 실패: 서버 오류거나 가입되지 않은 이메일입니다.");
            }
        }

        private async Task ExecuteVerifyCode()
        {
            if (string.IsNullOrWhiteSpace(Code))
            {
                MessageBox.Show("인증번호를 입력해주세요.");
                return;
            }

            bool success = await ApiService.VerifyCodeAsync(Email, Code);
            if (success)
            {
                MessageBox.Show("인증 성공!");
                IsVerified = true;
                FoundUsername = await ApiService.FindIdAsync(Email);
            }
            else
            {
                MessageBox.Show("인증번호가 틀렸습니다.");
            }
        }

        // ▼▼▼ [사용자님이 주신 코드가 여기 반영되었습니다] ▼▼▼
        private async Task ExecuteResetPassword()
        {
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                MessageBox.Show("새 비밀번호를 입력해주세요.");
                return;
            }

            if (NewPassword.Length < 8)
            {
                MessageBox.Show("비밀번호는 최소 8자 이상이어야 합니다.");
                return;
            }

            // [중요] 보안 수정 반영: Email + Code + NewPassword를 함께 전송
            // (ApiService.ResetPasswordAsync 메서드도 인자가 3개로 수정되어 있어야 합니다)
            bool success = await ApiService.ResetPasswordAsync(Email, Code, NewPassword);

            if (success)
            {
                MessageBox.Show("비밀번호가 변경되었습니다.\n로그인 화면으로 이동합니다.");
                _mainVM.NavigateToLogin();
            }
            else
            {
                MessageBox.Show("변경 실패: 인증 시간이 만료되었거나 오류가 발생했습니다.");
            }
        }

        // 화면 초기화용
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