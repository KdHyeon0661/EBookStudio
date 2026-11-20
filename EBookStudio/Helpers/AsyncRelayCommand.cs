using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace EBookStudio.Helpers
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Predicate<object?>? _canExecute;
        private readonly Action<Exception> _onError;
        private bool _isExecuting;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null,
                                 Action<Exception>? onError = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _onError = onError ?? ShowUnhandledError;
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute(parameter));
        }

        // 1. 기존 UI에서 호출하는 메서드 (기존 유지)
        public async void Execute(object? parameter)
        {
            try
            {
                await ExecuteAsync(parameter);
            }
            catch (Exception error)
            {
                _onError(error);
            }
        }

        // 2. [추가] 테스트 코드나 내부에서 await 할 수 있는 진짜 비동기 메서드
        public async Task ExecuteAsync(object? parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    _isExecuting = true;
                    CommandManager.InvalidateRequerySuggested();

                    await _execute(parameter);
                }
                finally
                {
                    _isExecuting = false;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private static void ShowUnhandledError(Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"[Command error] {error}");
            void Show() => MessageBox.Show(
                $"작업을 처리하는 중 오류가 발생했습니다.\n{error.Message}",
                "EBookStudio", MessageBoxButton.OK, MessageBoxImage.Error);

            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(Show);
            else if (Application.Current != null)
                Show();
        }
    }
}
