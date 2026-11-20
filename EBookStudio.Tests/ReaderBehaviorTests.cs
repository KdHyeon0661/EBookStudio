using System.Windows.Documents;
using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.ViewModels;
using Xunit;

namespace EBookStudio.Tests;

public sealed class ReaderBehaviorTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(7, 6)]
    public void OneBasedChapterMapsToZeroBasedSelection(int chapter, int selectedIndex)
    {
        Assert.Equal(selectedIndex, ReaderIndexMapper.ToSelectedIndex(chapter));
        Assert.Equal(chapter, ReaderIndexMapper.ToChapterIndex(selectedIndex));
    }

    [Theory]
    [InlineData(1, 100, 50, 1)]
    [InlineData(100, 100, 50, 50)]
    [InlineData(50, 100, 200, 99)]
    [InlineData(250, 100, 50, 50)]
    public void ProgressKeepsItsRelativePositionWhenPageCountChanges(
        int currentPage, int oldTotal, int newTotal, int expected)
    {
        Assert.Equal(expected, ReaderIndexMapper.MapProgressPage(currentPage, oldTotal, newTotal));
    }

    [Fact]
    public void StoredOffsetRestoresSecondRepeatedPhrase()
    {
        StaTest.Run(() =>
        {
            const string content = "repeat middle repeat";
            var document = new FlowDocument(new Paragraph(new Run(content)));
            int secondOffset = content.LastIndexOf("repeat", StringComparison.Ordinal);

            TextRange? found = TextRangeLocator.Find(document, "repeat", secondOffset);

            Assert.NotNull(found);
            Assert.Equal("repeat", found!.Text);
            Assert.Equal(secondOffset, TextRangeLocator.GetTextOffset(document, found.Start));
        });
    }

    [Fact]
    public void MissingOffsetFallsBackToFirstPhraseForLegacyNotes()
    {
        StaTest.Run(() =>
        {
            var document = new FlowDocument(new Paragraph(new Run("same then same")));

            TextRange? found = TextRangeLocator.Find(document, "same", -1);

            Assert.NotNull(found);
            Assert.Equal(0, TextRangeLocator.GetTextOffset(document, found!.Start));
        });
    }

    [Fact]
    public void AnnotationMovesToItsTextWhenReaderPagesChange()
    {
        var highlight = new NoteItem
        {
            Type = "Highlight",
            PageNumber = 2,
            TextOffset = 4,
            Content = "important"
        };

        bool changed = ReaderNoteMigrator.MigrateTextLocations(
            new[] { "title", "different", "an important passage" },
            new[] { highlight },
            Array.Empty<NoteItem>());

        Assert.True(changed);
        Assert.Equal(3, highlight.PageNumber);
        Assert.Equal(3, highlight.TextOffset);
    }

    [Fact]
    public void ResetProgressLeavesAddButtonUntouched()
    {
        var book = new Book { LastPage = 8, TotalPageCount = 20 };
        var addButton = new Book { IsAddButton = true, LastPage = 3 };

        LibraryViewModel.ResetReadingProgress(new[] { book, addButton });

        Assert.Equal(0, book.LastPage);
        Assert.Equal(3, addButton.LastPage);
    }
}
