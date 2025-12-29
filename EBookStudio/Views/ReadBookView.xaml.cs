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
                var noteData = NoteManager.LoadNotes(vm.CurrentUser, vm.BookTitle);
                var currentHighlights = noteData.Highlights.Where(x => x.PageNumber == vm.CurrentPageNum).ToList();
                var currentMemos = noteData.Memos.Where(x => x.PageNumber == vm.CurrentPageNum).ToList();

                if (BookViewer.Document == null) return;

                // 하이라이트 복구
                foreach (var hl in currentHighlights)
                {
                    TextRange foundRange = FindTextRangeInDocument(BookViewer.Document, hl.Content);
                    if (foundRange != null)
                    {
                        try
                        {
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
                        TextRange foundRange = FindTextRangeInDocument(BookViewer.Document, memo.OriginalText);
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
            if (sender is MenuItem menuItem && !BookViewer.Selection.IsEmpty)
            {
                string colorString = menuItem.Tag as string ?? "Transparent";

                if (colorString == "Underline")
                {
                    BookViewer.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
                }
                else if (colorString == "Transparent")
                {
                    BookViewer.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
                    BookViewer.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
                }
                else
                {
                    Brush highlightBrush = Brushes.Yellow;
                    try { highlightBrush = (Brush?)new BrushConverter().ConvertFromString(colorString) ?? Brushes.Yellow; } catch { }
                    BookViewer.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, highlightBrush);
                }

                if (DataContext is ReadBookViewModel vm && colorString != "Transparent")
                {
                    vm.SaveNoteData(new NoteItem
                    {
                        Type = "Highlight",
                        Color = colorString,
                        Content = BookViewer.Selection.Text.Trim()
                    });
                }
                BookViewer.Selection.Select(BookViewer.Selection.End, BookViewer.Selection.End);
            }
        }

        private void OnContextMenuMemoClick(object sender, RoutedEventArgs e)
        {
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

            if (_pendingMemoRange != null && !_pendingMemoRange.IsEmpty)
            {
                _isRendering = true;
                ApplyMemoLink(_pendingMemoRange, content);
                _isRendering = false;

                if (DataContext is ReadBookViewModel vm)
                {
                    vm.SaveNoteData(new NoteItem
                    {
                        Type = "Memo",
                        Content = content,
                        OriginalText = _pendingMemoRange.Text
                    });
                }
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
                link.Tag = content;
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

        private TextRange FindTextRangeInDocument(FlowDocument doc, string textToFind)
        {
            if (string.IsNullOrEmpty(textToFind)) return null;
            TextRange fullRange = new TextRange(doc.ContentStart, doc.ContentEnd);
            string fullText = fullRange.Text;
            int index = fullText.IndexOf(textToFind);
            if (index != -1)
            {
                TextPointer start = GetTextPointerAtOffset(doc.ContentStart, index);
                TextPointer end = GetTextPointerAtOffset(start, textToFind.Length);
                if (start != null && end != null) return new TextRange(start, end);
            }
            return null;
        }

        private TextPointer GetTextPointerAtOffset(TextPointer start, int offset)
        {
            if (start == null) return null;
            TextPointer current = start; int count = 0;
            while (current != null)
            {
                if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = current.GetTextInRun(LogicalDirection.Forward);
                    if (count + textRun.Length > offset) return current.GetPositionAtOffset(offset - count);
                    count += textRun.Length;
                }
                current = current.GetNextContextPosition(LogicalDirection.Forward);
                if (current != null && current.CompareTo(BookViewer.Document.ContentEnd) == 0) break;
            }
            return null;
        }

        private void BookViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) { _mouseDownPosition = e.GetPosition(this); _isDragging = false; }
        private void BookViewer_PreviewMouseMove(object sender, MouseEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed && (e.GetPosition(this) - _mouseDownPosition).Length > DragThreshold) _isDragging = true; }
        private void BookViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (!_isDragging && DataContext is ReadBookViewModel vm && vm.ToggleMenuCommand.CanExecute(null)) vm.ToggleMenuCommand.Execute(null); _isDragging = false; }
        private void OnCloseMemoViewClick(object sender, RoutedEventArgs e) => MemoViewOverlay.Visibility = Visibility.Collapsed;
        private void OnBorderClick(object sender, MouseButtonEventArgs e) => e.Handled = true;

        // =========================================================
        // [추가됨] 설정 팝업 이벤트 핸들러 (이게 없어서 에러 났음)
        // =========================================================

        // 설정 버튼 클릭 시 팝업 열기
        private void OnSettingButtonClick(object sender, RoutedEventArgs e)
        {
            if (SettingViewOverlay != null)
            {
                SettingViewOverlay.Visibility = Visibility.Visible;
            }
        }

        // 팝업 배경 클릭 시 닫기
        private void OnCloseSettingClick(object sender, RoutedEventArgs e)
        {
            if (SettingViewOverlay != null)
            {
                SettingViewOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }
}