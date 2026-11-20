namespace EBookStudio.Helpers
{
    internal static class ReaderIndexMapper
    {
        public static int ToSelectedIndex(int chapterIndex)
            => chapterIndex <= 0 ? -1 : chapterIndex - 1;

        public static int ToChapterIndex(int selectedIndex)
            => selectedIndex < 0 ? -1 : selectedIndex + 1;

        public static int MapProgressPage(int currentPage, int oldTotalPages, int newTotalPages)
        {
            if (newTotalPages <= 1) return 1;

            int safeCurrent = oldTotalPages > 0
                ? Math.Clamp(currentPage, 1, oldTotalPages)
                : 1;
            if (oldTotalPages <= 1) return Math.Clamp(safeCurrent, 1, newTotalPages);

            double ratio = (safeCurrent - 1d) / (oldTotalPages - 1d);
            int mapped = (int)Math.Round(ratio * (newTotalPages - 1d)) + 1;
            return Math.Clamp(mapped, 1, newTotalPages);
        }
    }
}
