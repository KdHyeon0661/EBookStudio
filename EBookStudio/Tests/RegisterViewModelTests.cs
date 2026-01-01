using Xunit;
using EBookStudio.ViewModels;
using EBookStudio.Helpers;
using System.Threading.Tasks;

namespace EBookStudio.Tests
{
    public class RegisterViewModelTests
    {
        private readonly RegisterViewModel _viewModel;
        private readonly FakeAuthService _fakeAuth;
        private readonly FakeDialogService _fakeDialog;
        private readonly MainViewModel _dummyMain;

        public RegisterViewModelTests()
        {
            _dummyMain = new MainViewModel();
            _fakeAuth = new FakeAuthService();
            _fakeDialog = new FakeDialogService();

            // 뷰모델 생성 시 가짜 서비스들을 주입
            _viewModel = new RegisterViewModel(_dummyMain, _fakeAuth, _fakeDialog);
        }

        [Fact]
        public async Task SendCode_FullProcess_Test()
        {
            // 1. [인증 코드 발송 테스트]
            _viewModel.Email = "test@example.com";
            _fakeAuth.SendCodeResult = true;

            if (_viewModel.SendCodeCommand is AsyncRelayCommand sendCmd)
                await sendCmd.ExecuteAsync(null);

            Assert.True(_viewModel.IsEmailSent);
            Assert.Contains("발송되었습니다", _fakeDialog.LastMessage);

            // 2. [인증 코드 확인 테스트]
            _viewModel.VerificationCode = "123456";
            _fakeAuth.VerifyCodeResult = true;

            if (_viewModel.VerifyCodeCommand is AsyncRelayCommand verifyCmd)
                await verifyCmd.ExecuteAsync(null);

            Assert.True(_viewModel.IsVerified);
            Assert.Contains("완료되었습니다", _fakeDialog.LastMessage);
        }

        [Fact]
        public async Task Register_Failure_PasswordMismatch_Test()
        {
            // Arrange: 이메일 인증은 이미 완료된 상태로 가정
            _viewModel.Username = "NewUser";
            _viewModel.Email = "test@example.com";
            _viewModel.IsVerified = true;

            // 서로 다른 비밀번호 배열 준비
            var passwords = new object[] { "pass123", "wrong456" };

            // Act
            if (_viewModel.RegisterCommand is AsyncRelayCommand regCmd)
                await regCmd.ExecuteAsync(passwords);

            // Assert
            Assert.Contains("일치하지 않습니다", _fakeDialog.LastMessage);
        }

        [Fact]
        public async Task Register_Success_Process_Test()
        {
            // Arrange
            _viewModel.Username = "SuccessUser";
            _viewModel.Email = "test@example.com";
            _viewModel.IsVerified = true; // 인증 필수
            _fakeAuth.RegisterResult = true; // 서버 가입 성공 가정

            // 동일한 비밀번호 배열
            var passwords = new object[] { "password123", "password123" };

            // Act
            if (_viewModel.RegisterCommand is AsyncRelayCommand regCmd)
                await regCmd.ExecuteAsync(passwords);

            // Assert
            Assert.Contains("가입 성공", _fakeDialog.LastMessage);
            // 메인 뷰가 로그인 화면으로 전환되었는지 확인
            Assert.IsType<LoginViewModel>(_dummyMain.CurrentView);
        }

        [Fact]
        public async Task Register_Failure_EmailNotVerified_ShouldNotExecute()
        {
            // Arrange: 이메일 인증을 하지 않은 상태
            _viewModel.Username = "NoVerifyUser";
            _viewModel.IsVerified = false;

            // Act
            // 커맨드가 실행 가능한 상태인지 확인
            bool canExecute = _viewModel.RegisterCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute); // IsVerified가 false이므로 실행 불가능(false)이어야 함
        }
    }
}