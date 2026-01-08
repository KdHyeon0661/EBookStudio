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

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<BookNoteData>(json) ?? new BookNoteData { BookTitle = bookFolder };
                }
                catch { }
            }
            return new BookNoteData { BookTitle = bookFolder };
        }

        public static void SaveNotes(string username, string bookFolder, BookNoteData data)
        {
            string path = FileHelper.GetLocalFilePath(username, bookFolder, "", "notes.json");

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(path, json);
        }

        public static void AddItem(string username, string bookFolder, NoteItem item)
        {
            var data = LoadNotes(username, bookFolder);

            if (item.Type == "Bookmark") data.Bookmarks.Insert(0, item);
            else if (item.Type == "Highlight") data.Highlights.Insert(0, item);
            else if (item.Type == "Memo") data.Memos.Insert(0, item);

            SaveNotes(username, bookFolder, data);
        }

        public static void RemoveItem(string username, string bookFolder, NoteItem item)
        {
            var data = LoadNotes(username, bookFolder);

            if (item.Type == "Bookmark")
                data.Bookmarks.RemoveAll(x => x.PageNumber == item.PageNumber);
            else if (item.Type == "Highlight")
                data.Highlights.RemoveAll(x => x.Content == item.Content && x.PageNumber == item.PageNumber);
            else if (item.Type == "Memo")
                data.Memos.RemoveAll(x => x.Content == item.Content && x.PageNumber == item.PageNumber);

            SaveNotes(username, bookFolder, data);
        }
    }
}