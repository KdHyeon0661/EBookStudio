using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EBookStudio.Views
{
    public partial class ReadBookView : UserControl
    {
        private Point _mouseDownPosition;
        private bool _isDragging = false;
        private const double DragThreshold = 5.0;
        private TextRange? _pendingMemoRange;
        private bool _isRendering = false;
        private bool _isContentChanged = false;

        public ReadBookView()
        {
            InitializeComponent();
            Unloaded += (s, e) =>
            {
                if (DataContext is ReadBookViewModel viewModel) viewModel.OnReaderClosed();
            };

            // 텍스트 변경 감지
            BookViewer.TextChanged += (s, e) => { if (!_isRendering) _isContentChanged = true; };

            // 화면 렌더링 후 복구 로직 실행
            BookViewer.LayoutUpdated += (s, e) =>
            {
                if (_isContentChanged)
                {
                    _isContentChanged = false;
                    LoadSavedVisuals();
                }
            };
        }

        // =========================================================
        // [복구 로직]
        // =========================================================
        private void LoadSavedVisuals()
        {
            if (DataContext is not ReadBookViewModel vm || string.IsNullOrEmpty(vm.CurrentUser)) return;
            _isRendering = true;
            try
            {
                var annotations = vm.GetCurrentPageAnnotations();
                var currentHighlights = annotations.Highlights;
                var currentMemos = annotations.Memos;

                if (BookViewer.Document == null) return;

                // 하이라이트 복구
                foreach (var hl in currentHighlights.Reverse())
                {
                    TextRange? foundRange = TextRangeLocator.Find(
                        BookViewer.Document, hl.Content, hl.TextOffset);
                    if (foundRange != null)
                    {
                        try
                        {
                            // Old note files could contain two styles for the same location.
                            // Clear both style families so the most recent saved annotation wins.
                            foundRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
                            foundRange.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
                            if (hl.Color == "Underline")
                                foundRange.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
                            else
                            {
                                var converter = new BrushConverter();
                                var brush = (Brush?)converter.ConvertFromString(hl.Color) ?? Brushes.Yellow;
                                foundRange.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
                            }
                        }
                        catch { }
                    }
                }

                // 메모 링크 복구
                foreach (var memo in currentMemos)
                {
                    if (!string.IsNullOrEmpty(memo.OriginalText))
                    {
                        TextRange? foundRange = TextRangeLocator.Find(
                            BookViewer.Document, memo.OriginalText, memo.TextOffset);
                        if (foundRange != null)
                            Dispatcher.Invoke(() => ApplyMemoLink(foundRange, memo.Content));
                    }
                }
            }
            catch { }
            finally { _isRendering = false; }
        }

        // =========================================================
        // [이벤트 핸들러] 하이라이트 & 메모
        // =========================================================
        private void OnHighlightColorClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && DataContext is ReadBookViewModel { IsBookLoaded: true } vm &&
                !BookViewer.Selection.IsEmpty)
            {
                string colorString = menuItem.Tag as string ?? "Transparent";
                string selectedText = BookViewer.Selection.Text.Trim();
                if (string.IsNullOrEmpty(selectedText)) return;
                int textOffset = GetSelectionTextOffset(BookViewer.Selection);
                var item = new NoteItem
                {
                    Type = "Highlight", Color = colorString, Content = selectedText, TextOffset = textOffset
                };
                bool saved = colorString == "Transparent" ? vm.RemoveNoteData(item) : vm.SaveNoteData(item);
                if (!saved) return;

                // A color change replaces the existing style instead of combining styles.
                BookViewer.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
                BookViewer.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);

                if (colorString == "Underline")
                {
                    BookViewer.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
                }
                else if (colorString == "Transparent")
                {
                    // The base style was already restored above.
                }
                else
                {
                    Brush highlightBrush = Brushes.Yellow;
                    try { highlightBrush = (Brush?)new BrushConverter().ConvertFromString(colorString) ?? Brushes.Yellow; } catch { }
                    BookViewer.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, highlightBrush);
                }

                BookViewer.Selection.Select(BookViewer.Selection.End, BookViewer.Selection.End);
            }
        }

        private void OnContextMenuMemoClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ReadBookViewModel { IsBookLoaded: true }) return;
            if (!BookViewer.Selection.IsEmpty)
            {
                _pendingMemoRange = new TextRange(BookViewer.Selection.Start, BookViewer.Selection.End);
                MemoTextBox.Text = "";
                MemoInputOverlay.Visibility = Visibility.Visible;
                MemoTextBox.Focus();
            }
            else MessageBox.Show("메모할 영역을 선택하세요.");
        }

        private void OnCancelMemoClick(object sender, RoutedEventArgs e)
        {
            MemoInputOverlay.Visibility = Visibility.Collapsed;
            _pendingMemoRange = null;
        }

        private void OnSaveMemoClick(object sender, RoutedEventArgs e)
        {
            string content = MemoTextBox.Text;
            if (string.IsNullOrWhiteSpace(content)) return;

            if (_pendingMemoRange != null && !_pendingMemoRange.IsEmpty && DataContext is ReadBookViewModel vm)
            {
                if (!vm.SaveNoteData(new NoteItem
                {
                    Type = "Memo", Content = content, OriginalText = _pendingMemoRange.Text,
                    TextOffset = GetTextOffset(_pendingMemoRange.Start)
                })) return;
                _isRendering = true;
                ApplyMemoLink(_pendingMemoRange, content);
                _isRendering = false;
            }
            MemoInputOverlay.Visibility = Visibility.Collapsed;
            _pendingMemoRange = null;
        }

        // =========================================================
        // [헬퍼 메서드]
        // =========================================================
        private void ApplyMemoLink(TextRange range, string content)
        {
            try
            {
                Hyperlink link = new Hyperlink(range.Start, range.End);
                link.Style = (Style)FindResource("MemoLinkStyle");

                // [수정됨] Tag와 ToolTip 모두에 내용을 저장합니다.
                link.Tag = content;
                link.ToolTip = content;
            }
            catch { }
        }

        private void OnMemoLinkClick(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink link && link.Tag is string content)
            {
                MemoContentBlock.Text = content;
                MemoViewOverlay.Visibility = Visibility.Visible;
                e.Handled = true;
            }
        }

        private int GetSelectionTextOffset(TextSelection selection)
        {
            string rawText = selection.Text;
            int leadingWhitespace = rawText.Length - rawText.TrimStart().Length;
            return GetTextOffset(selection.Start) + leadingWhitespace;
        }

        private int GetTextOffset(TextPointer position)
        {
            if (BookViewer.Document == null) return -1;
            return TextRangeLocator.GetTextOffset(BookViewer.Document, position);
        }

        private void BookViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) { _mouseDownPosition = e.GetPosition(this); _isDragging = false; }
        private void BookViewer_PreviewMouseMove(object sender, MouseEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed && (e.GetPosition(this) - _mouseDownPosition).Length > DragThreshold) _isDragging = true; }

        private void BookViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 드래그 중이었다면 클릭으로 처리하지 않음
            if (_isDragging)
            {
                _isDragging = false;
                return;
            }

            // 1. 클릭한 위치 가져오기
            Point mousePoint = e.GetPosition(BookViewer);
            TextPointer pointer = BookViewer.GetPositionFromPoint(mousePoint, true);

            if (pointer != null)
            {
                // [수정된 부분] 클릭한 요소부터 상위로 올라가며 Hyperlink 찾기
                DependencyObject current = pointer.Parent as DependencyObject;
                Hyperlink? hyperlink = null;

                while (current != null)
                {
                    if (current is Hyperlink link)
                    {
                        hyperlink = link;
                        break;
                    }
                    // 상위 요소로 이동 (TextElement인 경우 Parent 속성 사용)
                    if (current is TextElement te) current = te.Parent;
                    else if (current is FrameworkElement fe) current = fe.Parent;
                    else break;
                }

                // 하이퍼링크를 찾았다면 메모창 띄우기
                if (hyperlink != null)
                {
                    // Tag 또는 ToolTip에서 내용 가져오기
                    string? memoContent = hyperlink.Tag as string;
                    if (string.IsNullOrEmpty(memoContent))
                    {
                        memoContent = hyperlink.ToolTip?.ToString() ?? "내용 없음";
                    }

                    // 메모창 UI 업데이트 및 표시
                    if (MemoContentBlock != null) MemoContentBlock.Text = memoContent;
                    if (MemoViewOverlay != null) MemoViewOverlay.Visibility = Visibility.Visible;

                    // 메뉴 토글 등 다른 동작 막기
                    return;
                }
            }

            // 2. 하이퍼링크가 아니라면 기존처럼 메뉴바(상단/하단 바) 토글
            if (DataContext is ReadBookViewModel vm && vm.ToggleMenuCommand.CanExecute(null))
            {
                vm.ToggleMenuCommand.Execute(null);
            }
        }

        private void OnCloseMemoViewClick(object sender, RoutedEventArgs e) => MemoViewOverlay.Visibility = Visibility.Collapsed;
        private void OnBorderClick(object sender, MouseButtonEventArgs e) => e.Handled = true;

        // =========================================================
        // [설정 팝업 이벤트 핸들러]
        // =========================================================

        private void OnSettingButtonClick(object sender, RoutedEventArgs e)
        {
            if (SettingViewOverlay != null)
            {
                SettingViewOverlay.Visibility = Visibility.Visible;
            }
        }

        private void OnCloseSettingClick(object sender, RoutedEventArgs e)
        {
            if (SettingViewOverlay != null)
            {
                SettingViewOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }
}
