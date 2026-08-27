namespace EBookStudio.ViewModels
{
    public sealed record BookUsageDisplayItem(
        string Title,
        string BookId,
        string ReadingDuration,
        string ActivitySummary,
        double ProgressValue,
        string ProgressText,
        string LastReadText);

    public sealed record DailyUsageDisplayItem(
        string DateLabel,
        double AppBarHeight,
        double ReadingBarHeight,
        string AppDuration,
        string ReadingDuration,
        string TotalDuration);
}
