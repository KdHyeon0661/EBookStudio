using EBookStudio.Models;

namespace EBookStudio.Helpers
{
    internal static class ReaderNoteMigrator
    {
        public static bool MigrateTextLocations(
            IReadOnlyList<string> pages,
            IEnumerable<NoteItem> highlights,
            IEnumerable<NoteItem> memos)
        {
            bool changed = false;
            foreach (NoteItem item in highlights)
                changed |= Relocate(item, item.Content, pages);
            foreach (NoteItem item in memos)
                changed |= Relocate(item, item.OriginalText, pages);
            return changed;
        }

        private static bool Relocate(NoteItem item, string text, IReadOnlyList<string> pages)
        {
            if (string.IsNullOrEmpty(text) || pages.Count == 0) return false;

            if (item.PageNumber >= 1 && item.PageNumber <= pages.Count)
            {
                string currentPage = pages[item.PageNumber - 1];
                if (item.TextOffset >= 0 &&
                    item.TextOffset + text.Length <= currentPage.Length &&
                    string.CompareOrdinal(currentPage, item.TextOffset, text, 0, text.Length) == 0)
                {
                    return false;
                }
            }

            int bestPage = -1;
            int bestOffset = -1;
            int bestPageDistance = int.MaxValue;
            int bestOffsetDistance = int.MaxValue;

            for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                string page = pages[pageIndex];
                int searchFrom = 0;
                while (searchFrom <= page.Length - text.Length)
                {
                    int offset = page.IndexOf(text, searchFrom, StringComparison.Ordinal);
                    if (offset < 0) break;

                    int pageDistance = Math.Abs(pageIndex + 1 - item.PageNumber);
                    int offsetDistance = item.TextOffset >= 0
                        ? Math.Abs(offset - item.TextOffset)
                        : offset;
                    if (pageDistance < bestPageDistance ||
                        (pageDistance == bestPageDistance && offsetDistance < bestOffsetDistance))
                    {
                        bestPage = pageIndex + 1;
                        bestOffset = offset;
                        bestPageDistance = pageDistance;
                        bestOffsetDistance = offsetDistance;
                    }

                    searchFrom = offset + Math.Max(text.Length, 1);
                }
            }

            if (bestPage < 0) return false;
            bool changed = item.PageNumber != bestPage || item.TextOffset != bestOffset;
            item.PageNumber = bestPage;
            item.TextOffset = bestOffset;
            return changed;
        }
    }
}
