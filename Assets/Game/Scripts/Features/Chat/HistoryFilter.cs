using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Game.Features.Chat
{
    public enum HistoryRange
    {
        Last24Hours,
        Last3Days,
        ThisWeek,
        ThisMonth,
    }

    /// <summary>Filtering, ordering and display of the chat history list (newest first).</summary>
    public static class HistoryFilter
    {
        public const string UntitledChat = "Cuộc trò chuyện";

        public static readonly string[] RangeLabels = { "24 giờ qua", "3 ngày qua", "tuần này", "Tháng này" };

        public static string TitleOf(ChatHistoryItem item)
        {
            return string.IsNullOrWhiteSpace(item?.header) ? UntitledChat : item.header.Trim();
        }

        /// <summary>Parses the backend's ISO date; a date without a zone is taken as local time, one with a zone is converted.</summary>
        public static bool TryParseDate(string iso, out DateTime date)
        {
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date))
            {
                return false;
            }

            if (date.Kind == DateTimeKind.Utc)
            {
                date = date.ToLocalTime();
            }

            return true;
        }

        /// <summary>"08-03-2024 | 03:23 AM", or empty when the backend sent no usable date.</summary>
        public static string DisplayDate(string iso)
        {
            return TryParseDate(iso, out DateTime date) ? date.ToString("dd-MM-yyyy | hh:mm tt", CultureInfo.InvariantCulture) : string.Empty;
        }

        public static List<ChatHistoryItem> Apply(IEnumerable<ChatHistoryItem> items, HistoryRange? range, string query, DateTime now)
        {
            string needle = Normalize(query);
            return (items ?? Enumerable.Empty<ChatHistoryItem>())
                .Where(item => item != null)
                .Where(item => range == null || InRange(item, range.Value, now))
                .Where(item => needle.Length == 0 || Normalize(TitleOf(item)).Contains(needle))
                .OrderByDescending(item => TryParseDate(item.create_date, out DateTime date) ? date : DateTime.MinValue)
                .ToList();
        }

        private static bool InRange(ChatHistoryItem item, HistoryRange range, DateTime now)
        {
            if (!TryParseDate(item.create_date, out DateTime date))
            {
                return false;
            }

            switch (range)
            {
                case HistoryRange.Last24Hours: return now - date <= TimeSpan.FromHours(24);
                case HistoryRange.Last3Days: return now - date <= TimeSpan.FromDays(3);
                case HistoryRange.ThisWeek: return date >= StartOfWeek(now);
                default: return date.Year == now.Year && date.Month == now.Month;
            }
        }

        /// <summary>Monday 00:00 of the week containing <paramref name="now"/>.</summary>
        private static DateTime StartOfWeek(DateTime now)
        {
            int sinceMonday = ((int)now.DayOfWeek + 6) % 7;
            return now.Date.AddDays(-sinceMonday);
        }

        /// <summary>Lower case without accents, so "toa a4" finds "Tòa A4".</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string decomposed = text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
