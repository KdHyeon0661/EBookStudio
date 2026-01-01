using EBookStudio.Helpers;
using EBookStudio.ViewModels;
using Xunit;

namespace EBookStudio.Tests
{
    public class SettingViewModelTests
    {
        [Fact]
        public void SetThemeCommand_ShouldCallApplyDarkMode_WhenParameterIsDark()
        {
            // Arrange
            var fakeSettings = new FakeSettingsService();
            var viewModel = new SettingViewModel(fakeSettings);

            // Act
            viewModel.SetThemeCommand.Execute("Dark");

            // Assert
            Assert.True(fakeSettings.IsDarkModeCalled);
        }
    }
}
