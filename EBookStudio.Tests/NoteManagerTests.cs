using EBookStudio.Helpers;
using EBookStudio.Models;
using System.IO;
using Xunit;

namespace EBookStudio.Tests;

public sealed class NoteManagerTests
{
    [Fact]
    public void AnUnreadableNoteFileSurvivesAnAttemptToAddABookmark()
    {
        _ = TestEnvironment.Root;
        string user = TestEnvironment.UniqueName("notes");
        const string book = "damaged-notes";
        string path = FileHelper.GetLocalFilePath(user, book, "", "notes.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ incomplete");

        Assert.Throws<IOException>(() => NoteManager.AddItem(user, book, new NoteItem { PageNumber = 1 }));
        Assert.Equal("{ incomplete", File.ReadAllText(path));
    }

    [Fact]
    public void HighlightColorChangeReplacesSameTextLocation()
    {
        _ = TestEnvironment.Root;
        string user = TestEnvironment.UniqueName("note-user");
        string book = TestEnvironment.UniqueName("book");

        NoteManager.AddItem(user, book, Highlight("same", 10, "Yellow"));
        NoteManager.AddItem(user, book, Highlight("same", 10, "Red"));

        var notes = NoteManager.LoadNotes(user, book);
        NoteItem highlight = Assert.Single(notes.Highlights);
        Assert.Equal("Red", highlight.Color);
        Assert.Equal(10, highlight.TextOffset);
    }

    [Fact]
    public void RepeatedTextAtDifferentOffsetsCanBeRemovedIndependently()
    {
        _ = TestEnvironment.Root;
        string user = TestEnvironment.UniqueName("repeat-user");
        string book = TestEnvironment.UniqueName("book");
        NoteItem first = Highlight("repeat", 5, "Yellow");
        NoteItem second = Highlight("repeat", 30, "Blue");

        NoteManager.AddItem(user, book, first);
        NoteManager.AddItem(user, book, second);
        NoteManager.RemoveItem(user, book, first);

        NoteItem remaining = Assert.Single(NoteManager.LoadNotes(user, book).Highlights);
        Assert.Equal(30, remaining.TextOffset);
        Assert.Equal("Blue", remaining.Color);
    }

    private static NoteItem Highlight(string content, int offset, string color) => new()
    {
        Type = "Highlight",
        PageNumber = 1,
        Content = content,
        TextOffset = offset,
        Color = color
    };
}
