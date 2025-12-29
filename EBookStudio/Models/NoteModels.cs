using System;
using System.Collections.Generic;

namespace EBookStudio.Models
{
    // 노트 데이터 전체 구조
    public class BookNoteData
    {
        public string BookTitle { get; set; } = string.Empty;
        public List<NoteItem> Bookmarks { get; set; } = new List<NoteItem>();
        public List<NoteItem> Highlights { get; set; } = new List<NoteItem>();
        public List<NoteItem> Memos { get; set; } = new List<NoteItem>();
    }

    // 개별 아이템
    public class NoteItem
    {
        public string Type { get; set; } = "Bookmark"; // Bookmark, Highlight, Memo
        public int PageNumber { get; set; }      // 페이지 번호
        public string Content { get; set; } = string.Empty; // 내용 (하이라이트된 텍스트 or 메모)
        public string OriginalText { get; set; } = string.Empty; // (메모용) 원문
        public string Color { get; set; } = string.Empty;   // (하이라이트용) 색상
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string DateString => CreatedAt.ToString("yyyy.MM.dd HH:mm");
    }
}