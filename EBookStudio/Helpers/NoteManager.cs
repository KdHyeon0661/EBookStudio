using EBookStudio.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EBookStudio.Helpers
{
    public static class NoteManager
    {
        // 로드
        public static BookNoteData LoadNotes(string username, string bookTitle)
        {
            // [수정] "notes" -> "" (빈 문자열)로 변경하여 하위 폴더 생성 방지
            string path = FileHelper.GetLocalFilePath(username, bookTitle, "", "notes.json");

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<BookNoteData>(json) ?? new BookNoteData { BookTitle = bookTitle };
                }
                catch { }
            }
            return new BookNoteData { BookTitle = bookTitle };
        }

        // 저장
        public static void SaveNotes(string username, string bookTitle, BookNoteData data)
        {
            // [수정] "notes" -> "" (빈 문자열)로 변경
            string path = FileHelper.GetLocalFilePath(username, bookTitle, "", "notes.json");

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(path, json);
        }

        // 추가
        public static void AddItem(string username, string bookTitle, NoteItem item)
        {
            var data = LoadNotes(username, bookTitle);

            if (item.Type == "Bookmark") data.Bookmarks.Insert(0, item);
            else if (item.Type == "Highlight") data.Highlights.Insert(0, item);
            else if (item.Type == "Memo") data.Memos.Insert(0, item);

            SaveNotes(username, bookTitle, data);
        }

        // 삭제
        public static void RemoveItem(string username, string bookTitle, NoteItem item)
        {
            var data = LoadNotes(username, bookTitle);

            // 리스트에서 항목 제거
            if (item.Type == "Bookmark")
                data.Bookmarks.RemoveAll(x => x.PageNumber == item.PageNumber);
            else if (item.Type == "Highlight")
                data.Highlights.RemoveAll(x => x.Content == item.Content && x.PageNumber == item.PageNumber);
            else if (item.Type == "Memo")
                data.Memos.RemoveAll(x => x.Content == item.Content && x.PageNumber == item.PageNumber);

            SaveNotes(username, bookTitle, data);
        }
    }
}