using EBookStudio.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EBookStudio.Helpers
{
    public static class NoteManager
    {
        public static BookNoteData LoadNotes(string username, string bookFolder)
        {
            string path = FileHelper.GetLocalFilePath(username, bookFolder, "", "notes.json");

            try
            {
                if (!File.Exists(path) && !Directory.EnumerateFileSystemEntries(
                    Path.GetDirectoryName(path)!, Path.GetFileName(path),
                    new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0 }).Any())
                    return new BookNoteData { BookTitle = bookFolder };
                string json = File.ReadAllText(path);
                var notes = JsonSerializer.Deserialize<BookNoteData>(json);
                if (notes == null || !Valid(notes.Bookmarks) || !Valid(notes.Highlights) || !Valid(notes.Memos))
                    throw new JsonException("노트 파일 형식이 올바르지 않습니다.");
                return notes;
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            { return new BookNoteData { BookTitle = bookFolder }; }
            catch (JsonException error)
            { throw new IOException("기존 노트 파일을 읽을 수 없습니다.", error); }
        }

        private static bool Valid(List<NoteItem>? items) => items != null && items.All(item => item != null);

        public static void SaveNotes(string username, string bookFolder, BookNoteData data)
        {
            string path = FileHelper.GetLocalFilePath(username, bookFolder, "", "notes.json");

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            AtomicFile.WriteAllText(path, json);
        }

        public static void AddItem(string username, string bookFolder, NoteItem item)
        {
            var data = LoadNotes(username, bookFolder);

            if (item.Type == "Bookmark")
            {
                data.Bookmarks.RemoveAll(x => x.PageNumber == item.PageNumber);
                data.Bookmarks.Insert(0, item);
            }
            else if (item.Type == "Highlight")
            {
                data.Highlights.RemoveAll(x => SameTextLocation(x, item, useOriginalText: false));
                data.Highlights.Insert(0, item);
            }
            else if (item.Type == "Memo")
            {
                data.Memos.RemoveAll(x => SameTextLocation(x, item, useOriginalText: true));
                data.Memos.Insert(0, item);
            }

            SaveNotes(username, bookFolder, data);
        }

        public static void RemoveItem(string username, string bookFolder, NoteItem item)
        {
            var data = LoadNotes(username, bookFolder);

            if (item.Type == "Bookmark")
                data.Bookmarks.RemoveAll(x => x.PageNumber == item.PageNumber);
            else if (item.Type == "Highlight")
                data.Highlights.RemoveAll(x => SameTextLocation(x, item, useOriginalText: false));
            else if (item.Type == "Memo")
                data.Memos.RemoveAll(x => SameTextLocation(x, item, useOriginalText: true));

            SaveNotes(username, bookFolder, data);
        }

        private static bool SameTextLocation(NoteItem existing, NoteItem target, bool useOriginalText)
        {
            if (existing.PageNumber != target.PageNumber) return false;
            string existingText = useOriginalText ? existing.OriginalText : existing.Content;
            string targetText = useOriginalText ? target.OriginalText : target.Content;
            if (!string.Equals(existingText, targetText, StringComparison.Ordinal)) return false;

            // Legacy notes did not store an offset. Keep them removable by their old page/text key.
            return existing.TextOffset < 0 || target.TextOffset < 0 || existing.TextOffset == target.TextOffset;
        }
    }
}
