using Xunit;
using EBookStudio.ViewModels;
using EBookStudio.Helpers;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace EBookStudio.Tests
{
    public class LoginViewModelTests
    {
        private readonly LoginViewModel _viewModel;
        private readonly FakeAuthService _fakeAuth;
        private readonly FakeDialogService _fakeDialog;
        private readonly MainViewModel _dummyMain;

        public LoginViewModelTests()
        {
            _dummyMain = new MainViewModel();
            _fakeAuth = new FakeAuthService();
            _fakeDialog = new FakeDialogService();

            // 생성자 주입을 통해 가짜 객체들 전달
            _viewModel = new LoginViewModel(_dummyMain, _fakeAuth, _fakeDialog);
        }

        [UIFact]
        public async Task Login_WithValidCredentials_ShouldSucceed()
        {
            // 1. Arrange
            _viewModel.Username = "TestUser";
            _fakeAuth.LoginResult = true;

            // [핵심] 실제 PasswordBox 객체를 만들고 비밀번호를 채웁니다.
            var passwordBox = new PasswordBox();
            passwordBox.Password = "password123";

            // 2. Act: null 대신 위에서 만든 passwordBox를 파라미터로 전달합니다.
            if (_viewModel.LoginCommand is AsyncRelayCommand loginCmd)
            {
                await loginCmd.ExecuteAsync(passwordBox);
            }

            // 3. Assert
            Assert.Contains("로그인 성공", _fakeDialog.LastMessage);
            Assert.Equal("TestUser", _dummyMain.LoggedInUser);
        }

        [UIFact]
        public async Task Login_WithInvalidCredentials_ShouldShowErrorMessage()
        {
            // 1. Arrange
            _viewModel.Username = "WrongUser";
            _fakeAuth.LoginResult = false;

            var passwordBox = new PasswordBox();
            passwordBox.Password = "wrongpass";

            // 2. Act
            if (_viewModel.LoginCommand is AsyncRelayCommand loginCmd)
            {
                await loginCmd.ExecuteAsync(passwordBox);
            }

            // 3. Assert: 이제 유효성 검사를 통과하므로 "로그인 실패" 메시지가 뜹니다.
            Assert.Contains("로그인 실패", _fakeDialog.LastMessage);
        }

        [Fact]
        public async Task Login_EmptyUsername_ShouldShowWarning()
        {
            // 1. Arrange: 아이디를 입력하지 않음
            _viewModel.Username = "";

            // 2. Act
            if (_viewModel.LoginCommand is AsyncRelayCommand loginCmd)
            {
                await loginCmd.ExecuteAsync(null);
            }

            // 3. Assert
            Assert.Contains("입력하세요", _fakeDialog.LastMessage);
        }
    }
}