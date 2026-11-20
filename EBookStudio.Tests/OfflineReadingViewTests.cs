using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.ViewModels;
using EBookStudio.Views;
using Xunit;

namespace EBookStudio.Tests;

public sealed class OfflineReadingViewTests
{
    [Fact]
    public void ReadCommandOpensCachedBookAndShowsItsSavedPageWhileLoggedOut()
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            ReadBookViewModel? reader = null;

            try
            {
                string user = TestEnvironment.UniqueName("offline-user");
                string folder = TestEnvironment.UniqueName("offline-book");
                string music = TestEnvironment.UniqueName("offline-music") + ".wav";
                var book = new Book { FolderId = folder, Title = "Offline book",
                    FileName = "book_full.json", IsAvailable = true };
                Directory.CreateDirectory(FileHelper.GetBookDirectory(user, folder));
                Directory.CreateDirectory(FileHelper.MusicBasePath);
                WriteSilentWav(Path.Combine(FileHelper.MusicBasePath, music));
                File.WriteAllText(FileHelper.GetLocalFilePath(user, folder, "", book.FileName), $$"""
                    {"chapters":[{"chapter_index":1,"title":"Chapter one","segments":[
                        {"music_path":"music/{{music}}","pages":[{"text":"Saved page body."}]}
                    ]}]}
                    """);
                ReadingProgressManager.SaveProgress(user, folder, 2, 2);

                var main = new MainViewModel { LoggedInUser = user };
                Assert.False(main.IsLoggedIn);
                Task libraryLoad = main.LibraryVM.LoadLibrary();
                Assert.True(StaTest.PumpUntil(() => libraryLoad.IsCompleted));
                libraryLoad.GetAwaiter().GetResult();
                Book cached = Assert.Single(main.LibraryVM.DisplayBooks, item => !item.IsAddButton);
                main.LibraryVM.OpenBookCommand.Execute(cached);
                var details = Assert.IsType<BookDetailViewModel>(main.CurrentView);
                Assert.True(details.ReadCommand.CanExecute(null));
                details.ReadCommand.Execute(null);
                reader = Assert.IsType<ReadBookViewModel>(main.CurrentView);
                var view = new ReadBookView { DataContext = reader };

                WaitForPage(reader);
                view.Measure(new Size(1150, 768));
                view.Arrange(new Rect(0, 0, 1150, 768));
                view.UpdateLayout();

                Assert.Equal(2, reader.CurrentPageNum);
                Assert.Equal(2, reader.TotalPages);
                var textBox = Assert.IsType<RichTextBox>(view.FindName("BookViewer"));
                var text = new TextRange(textBox.Document.ContentStart, textBox.Document.ContentEnd).Text;
                Assert.Equal("Saved page body.", text.Trim());
                dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.False(reader.IsMusicPlaying);

                reader.ToggleMusicCommand.Execute(null);
                StaTest.PumpUntil(() => reader.IsMusicPlaying);
                Assert.True(reader.IsMusicPlaying);
                reader.ToggleMusicCommand.Execute(null);
                Assert.False(reader.IsMusicPlaying);
                reader.PrevPageCommand.Execute(null);
                reader.NextPageCommand.Execute(null);
                Assert.Equal(2, reader.CurrentPageNum);
                Assert.False(reader.IsMusicPlaying);

                reader.OpenNoteCommand.Execute(null);
                var notes = Assert.IsType<NoteViewModel>(main.CurrentView);
                notes.GoToPageCommand.Execute(new NoteItem { Type = "Bookmark", PageNumber = 1 });
                reader = Assert.IsType<ReadBookViewModel>(main.CurrentView);
                WaitForPage(reader, "=== Chapter one ===");
                Assert.Equal(1, reader.CurrentPageNum);
            }
            finally
            {
                reader?.OnReaderClosed();
                app.Shutdown();
                dispatcher.InvokeShutdown();
            }
        });
    }

    private static void WaitForPage(ReadBookViewModel reader, string expected = "Saved page body.")
    {
        StaTest.PumpUntil(() => reader.CurrentPageContent == expected ||
            reader.CurrentPageContent.StartsWith("오류", StringComparison.Ordinal));
        Assert.Equal(expected, reader.CurrentPageContent);
    }

    private static void WriteSilentWav(string path)
    {
        const int sampleRate = 8000;
        const int bytesPerSample = 2;
        const int dataSize = sampleRate * bytesPerSample;
        using var writer = new BinaryWriter(File.OpenWrite(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * bytesPerSample);
        writer.Write((short)bytesPerSample);
        writer.Write((short)(bytesPerSample * 8));
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);
    }
}
