using System.Windows;
using System.Windows.Input;
using EBookStudio.Models;
using EBookStudio.Helpers;

namespace EBookStudio.ViewModels
{
    public class FindAccountViewModel : ViewModelBase
    {
        // --- 입력 데이터 ---
        public string Email { get; set; }
        public string Code { get; set; }
        public string NewPassword { get; set; }

        // --- 상태 관리 (화면 보이게/숨기게) ---
        private bool _isCodeSent; // 인증번호 보냈니?
        public bool IsCodeSent
        {
            get => _isCodeSent;
            set { _isCodeSent = value; OnPropertyChanged(); }
        }

        private bool _isVerified; // 인증 성공했니?
        public bool IsVerified
        {
            get => _isVerified;
            set { _isVerified = value; OnPropertyChanged(); }
        }

        private string? _foundUsername; // 찾은 아이디
        public string? FoundUsername
        {
            get => _foundUsername;
            set { _foundUsername = value; OnPropertyChanged(); }
        }

        // --- 명령어 ---
        public ICommand SendCodeCommand { get; }
        public ICommand VerifyCodeCommand { get; }
        public ICommand ResetPasswordCommand { get; }

        private readonly MainViewModel _mainVM;
        public ICommand GoBackCommand { get; }

        public FindAccountViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;

            Email = string.Empty;
            Code = string.Empty;
            NewPassword = string.Empty;

            GoBackCommand = new RelayCommand(o => _mainVM.NavigateToLogin());

            SendCodeCommand = new RelayCommand(async o =>
            {
                if (string.IsNullOrWhiteSpace(Email)) return;
                bool success = await ApiService.SendCodeAsync(Email);
                if (success)
                {
                    MessageBox.Show("인증번호가 발송되었습니다.");
                    IsCodeSent = true; // 인증번호 입력창 보여줌
                }
                else MessageBox.Show("이메일 전송 실패");
            });

            VerifyCodeCommand = new RelayCommand(async o =>
            {
                bool success = await ApiService.VerifyCodeAsync(Email, Code);
                if (success)
                {
                    MessageBox.Show("인증 성공!");
                    IsVerified = true; // 결과창 보여줌

                    // 인증 되자마자 아이디 찾아오기
                    FoundUsername = await ApiService.FindIdAsync(Email);
                }
                else MessageBox.Show("인증번호가 틀렸습니다.");
            });

            ResetPasswordCommand = new RelayCommand(async o =>
            {
                if (string.IsNullOrWhiteSpace(NewPassword)) return;
                bool success = await ApiService.ResetPasswordAsync(Email, NewPassword);
                if (success)
                {
                    MessageBox.Show("비밀번호가 변경되었습니다. 로그인해주세요.");

                    // [수정] CloseAction -> 로그인 화면으로 이동
                    _mainVM.NavigateToLogin();
                }
                else MessageBox.Show("변경 실패");
            });
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