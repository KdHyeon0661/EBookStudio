using System.Windows.Documents;

namespace EBookStudio.Helpers
{
    internal static class TextRangeLocator
    {
        public static TextRange? Find(FlowDocument document, string text, int textOffset)
        {
            if (string.IsNullOrEmpty(text)) return null;

            if (textOffset >= 0)
            {
                TextPointer? storedStart = GetTextPointerAtOffset(
                    document, document.ContentStart, textOffset);
                TextPointer? storedEnd = storedStart == null
                    ? null : GetTextPointerAtOffset(document, storedStart, text.Length);
                if (storedStart != null && storedEnd != null)
                {
                    var storedRange = new TextRange(storedStart, storedEnd);
                    if (string.Equals(storedRange.Text, text, StringComparison.Ordinal))
                        return storedRange;
                }
            }

            var fullRange = new TextRange(document.ContentStart, document.ContentEnd);
            int fallbackIndex = fullRange.Text.IndexOf(text, StringComparison.Ordinal);
            if (fallbackIndex < 0) return null;
            TextPointer? fallbackStart = GetTextPointerAtOffset(
                document, document.ContentStart, fallbackIndex);
            TextPointer? fallbackEnd = fallbackStart == null
                ? null : GetTextPointerAtOffset(document, fallbackStart, text.Length);
            return fallbackStart != null && fallbackEnd != null
                ? new TextRange(fallbackStart, fallbackEnd)
                : null;
        }

        public static int GetTextOffset(FlowDocument document, TextPointer position)
            => new TextRange(document.ContentStart, position).Text.Length;

        private static TextPointer? GetTextPointerAtOffset(
            FlowDocument document, TextPointer start, int offset)
        {
            TextPointer? current = start;
            int count = 0;
            while (current != null)
            {
                if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = current.GetTextInRun(LogicalDirection.Forward);
                    if (count + textRun.Length >= offset)
                        return current.GetPositionAtOffset(offset - count);
                    count += textRun.Length;
                }
                current = current.GetNextContextPosition(LogicalDirection.Forward);
                if (current != null && current.CompareTo(document.ContentEnd) == 0) break;
            }
            return null;
        }
    }
}
