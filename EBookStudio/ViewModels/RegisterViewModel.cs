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

        // [수정] 선언과 동시에 빈 값으로 초기화합니다.
        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _verificationCode = string.Empty;

        private bool _isEmailSent = false;
        private bool _isVerified = false;

        // --- 속성 추가 ---
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Email // 이메일 필드 추가
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string VerificationCode // 인증 코드 필드 추가
        {
            get => _verificationCode;
            set { _verificationCode = value; OnPropertyChanged(); }
        }

        public bool IsEmailSent // 이메일이 발송되었는지 여부
        {
            get => _isEmailSent;
            set { _isEmailSent = value; OnPropertyChanged(); }
        }

        public bool IsVerified // 이메일 인증이 완료되었는지 여부
        {
            get => _isVerified;
            set { _isVerified = value; OnPropertyChanged(); }
        }

        // --- 명령어 추가 ---
        public ICommand RegisterCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand SendCodeCommand { get; } // 인증 코드 발송 명령어 추가
        public ICommand VerifyCodeCommand { get; } // 인증 코드 확인 명령어 추가

        public RegisterViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            RegisterCommand = new RelayCommand(async (o) => await ExecuteRegister(o), CanRegister);
            BackCommand = new RelayCommand(o => _mainVM.NavigateToLogin());
            SendCodeCommand = new RelayCommand(async (o) => await ExecuteSendCode());
            VerifyCodeCommand = new RelayCommand(async (o) => await ExecuteVerifyCode());
        }

        // --- 인증 코드 발송 로직 (백엔드와 통신) ---
        private async Task ExecuteSendCode()
        {
            if (string.IsNullOrWhiteSpace(Email) || !Email.Contains("@"))
            {
                MessageBox.Show("유효한 이메일 주소를 입력해주세요.");
                return;
            }

            // 서버에 인증 코드 발송 요청 (가상)
            bool success = await ApiService.SendVerificationCodeAsync(Email);

            if (success)
            {
                IsEmailSent = true;
                MessageBox.Show("인증 코드가 이메일로 발송되었습니다. (가상)");
            }
            else
            {
                MessageBox.Show("인증 코드 발송에 실패했습니다. 이메일을 확인하거나 잠시 후 다시 시도해주세요.");
            }
        }

        // --- 인증 코드 확인 로직 (백엔드와 통신) ---
        private async Task ExecuteVerifyCode()
        {
            if (string.IsNullOrWhiteSpace(VerificationCode))
            {
                MessageBox.Show("인증 코드를 입력해주세요.");
                return;
            }

            // 서버에 인증 코드 확인 요청 (가상)
            bool success = await ApiService.VerifyCodeAsync(Email, VerificationCode);

            if (success)
            {
                IsVerified = true;
                MessageBox.Show("이메일 인증이 완료되었습니다!");
                // 비밀번호 재확인 조건이 바뀔 수 있으므로 RegisterCommand의 CanExecute를 재평가
                CommandManager.InvalidateRequerySuggested();
            }
            else
            {
                MessageBox.Show("인증 코드가 일치하지 않거나 만료되었습니다.");
                IsVerified = false;
            }
        }

        // --- 회원가입 버튼 활성화 조건 ---
        private bool CanRegister(object? parameter)
        {
            // 비밀번호 재입력 (PasswordBox)의 내용을 가져올 수 없어, 여기서 모든 유효성 검사를 할 수는 없습니다.
            // 일단 이메일 인증이 완료되어야 활성화되도록 합니다.
            return IsVerified;
        }

        // --- 회원가입 최종 실행 로직 (유효성 검사 강화) ---
        private async Task ExecuteRegister(object? parameter)
        {
            // 1. 넘어온 파라미터가 '배열'인지 확인
            var passwords = parameter as object[];

            if (passwords == null || passwords.Length < 2)
            {
                MessageBox.Show("비밀번호 입력 값을 불러올 수 없습니다.");
                return;
            }

            // 2. 배열에서 비밀번호 두 개 꺼내기
            string? password = passwords[0] as string;
            string? confirmPassword = passwords[1] as string;

            // 3. 유효성 검사
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                MessageBox.Show("모든 항목을 입력해주세요.");
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("비밀번호와 비밀번호 확인이 일치하지 않습니다.");
                return;
            }

            if (!IsVerified)
            {
                MessageBox.Show("이메일 인증을 완료해주세요.");
                return;
            }

            // 4. 서버로 전송
            bool success = await ApiService.RegisterAsync(Username, password, Email);

            if (success)
            {
                MessageBox.Show("가입 성공! 로그인 화면으로 이동합니다.");
                _mainVM.NavigateToLogin();
            }
            else
            {
                MessageBox.Show("가입 실패: 이미 존재하는 아이디거나 서버 오류입니다.");
            }
        }

        public void Clear()
        {
            Username = string.Empty;       // 프로퍼티(대문자)에 대입해야 OnPropertyChanged가 발생함
            Email = string.Empty;
            VerificationCode = string.Empty;
            IsEmailSent = false;
            IsVerified = false;
        }
    }
}