using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using EBookStudio.Models;
using EBookStudio.ViewModels;
using EBookStudio.Views;
using Xunit;

namespace EBookStudio.Tests;

public sealed class ReadingProgressViewTests
{
    [Fact]
    public void AccountRecoveryPassesAMaskedPasswordBoxToTheResetCommand()
    {
        StaTest.Run(() =>
        {
            var view = new FindAccountView();
            var input = Assert.IsType<PasswordBox>(view.FindName("NewPasswordBox"));
            var button = Assert.Single(FindDescendants<Button>(view),
                button => Equals(button.Content, "비밀번호 변경하기"));
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Assert.Same(input, button.CommandParameter);
        });
    }

    [Fact]
    public void BookDetailsDisplayAndUpdateComputedProgress()
    {
        StaTest.Run(() =>
        {
            var book = new Book { LastPage = 5, TotalPageCount = 20 };
            var view = new BookDetailView { DataContext = new { Book = book } };
            var bar = Assert.Single(FindDescendants<ProgressBar>(view), bar => !bar.IsIndeterminate);

            Assert.Equal(BindingMode.OneWay,
                BindingOperations.GetBinding(bar, ProgressBar.ValueProperty)?.Mode);
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Assert.Equal(25, bar.Value);
            book.LastPage = 12;
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Assert.Equal(60, bar.Value);
        });
    }

    [Fact]
    public void MyPageDisplaysBookUsageProgressWithOneWayBinding()
    {
        StaTest.Run(() =>
        {
            var view = new MyPageView();
            var bookUsageList = Assert.Single(FindDescendants<ItemsControl>(view), list =>
                BindingOperations.GetBinding(list, ItemsControl.ItemsSourceProperty)?.Path.Path
                    == nameof(MyPageViewModel.BookUsageItems));
            var row = Assert.IsAssignableFrom<FrameworkElement>(bookUsageList.ItemTemplate.LoadContent());
            row.DataContext = new BookUsageDisplayItem("Sample", "book-1", "10분", "2회", 65, "65%", "오늘");
            var bar = Assert.Single(FindDescendants<ProgressBar>(row));

            var binding = Assert.IsType<Binding>(BindingOperations.GetBinding(bar, ProgressBar.ValueProperty));
            Assert.Equal(BindingMode.OneWay, binding.Mode);
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Assert.Equal(65, bar.Value);
        });
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match) yield return match;
            foreach (var descendant in FindDescendants<T>(child)) yield return descendant;
        }
    }
}
