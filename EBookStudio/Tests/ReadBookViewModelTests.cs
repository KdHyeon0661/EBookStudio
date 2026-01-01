using Xunit; // xUnit 필수
using EBookStudio.ViewModels;
using EBookStudio.Models;
using System.Threading;

namespace EBookStudio.Tests
{
    public class ReadBookViewModelTests
    {
        private ReadBookViewModel _viewModel;
        private Book _dummyBook;
        private MainViewModel _dummyMainVM;

        // xUnit에서는 생성자가 초기화(SetUp) 역할을 합니다.
        public ReadBookViewModelTests()
        {
            // 1. 가짜 데이터 생성
            _dummyMainVM = new MainViewModel();
            _dummyBook = new Book
            {
                Title = "Test Book",
                TotalPageCount = 10,
                FileName = "test_book.json"
            };

            // 2. ViewModel 초기화
            _viewModel = new ReadBookViewModel(_dummyMainVM, _dummyBook);

            // 3. 테스트 초기값 설정
            _viewModel.TotalPages = 10;
            _viewModel.CurrentPageNum = 1;
        }

        // 기본 [Fact]를 사용합니다.
        [Fact]
        public void NextPageCommand_WhenExecuted_ShouldIncreasePageNum()
        {
            // Arrange
            _viewModel.CurrentPageNum = 1;

            // Act
            if (_viewModel.NextPageCommand.CanExecute(null))
            {
                _viewModel.NextPageCommand.Execute(null);
            }

            // Assert
            Assert.Equal(2, _viewModel.CurrentPageNum);
        }

        [Fact]
        public void NextPageCommand_AtLastPage_ShouldNotIncrease()
        {
            // Arrange
            _viewModel.CurrentPageNum = 10;

            // Act
            _viewModel.NextPageCommand.Execute(null);

            // Assert
            Assert.Equal(10, _viewModel.CurrentPageNum);
        }

        [Fact]
        public void PrevPageCommand_WhenExecuted_ShouldDecreasePageNum()
        {
            // Arrange
            _viewModel.CurrentPageNum = 5;

            // Act
            _viewModel.PrevPageCommand.Execute(null);

            // Assert
            Assert.Equal(4, _viewModel.CurrentPageNum);
        }

        [Fact]
        public void PrevPageCommand_AtFirstPage_ShouldNotDecrease()
        {
            // Arrange
            _viewModel.CurrentPageNum = 1;

            // Act
            _viewModel.PrevPageCommand.Execute(null);

            // Assert
            Assert.Equal(1, _viewModel.CurrentPageNum);
        }

        [Fact]
        public void PageStatus_ShouldFormatCorrectly()
        {
            // Arrange
            _viewModel.TotalPages = 20;
            _viewModel.CurrentPageNum = 5;

            // Act
            string status = _viewModel.PageStatus;

            // Assert
            Assert.Equal("5 / 20", status);
        }

        [Fact]
        public void IsMusicEnabled_WhenSetFalse_ShouldStopMusic()
        {
            // Arrange
            _viewModel.IsMusicPlaying = true;

            // Act
            _viewModel.IsMusicEnabled = false;

            // Assert
            Assert.False(_viewModel.IsMusicPlaying);
        }
    }
}