using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Features.Auth
{
    /// <summary>Birthday text for the form (dd/MM/yyyy, as in the Figma) and for the backend (ISO, midnight UTC).</summary>
    public static class BirthdayFormat
    {
        /// <summary>The year list stops this many years before today and goes back <see cref="YearSpan"/> years.</summary>
        public const int MinAge = 5;
        public const int YearSpan = 95;

        public static string Display(DateTime date)
        {
            return date.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);
        }

        public static string Backend(DateTime date)
        {
            return date.ToString("yyyy-MM-dd'T'00:00:00.000'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>Builds the date, pulling the day back when the month is shorter (31 Feb becomes 28/29 Feb).</summary>
        public static DateTime Clamp(int year, int month, int day)
        {
            return new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
        }

        public static List<string> Days()
        {
            var days = new List<string>(31);
            for (int day = 1; day <= 31; day++)
            {
                days.Add(day.ToString("00", CultureInfo.InvariantCulture));
            }

            return days;
        }

        public static List<string> Months()
        {
            var months = new List<string>(12);
            for (int month = 1; month <= 12; month++)
            {
                months.Add("Tháng " + month.ToString(CultureInfo.InvariantCulture));
            }

            return months;
        }

        /// <summary>Newest first, so a typical user scrolls a short way.</summary>
        public static List<int> Years(DateTime today)
        {
            int latest = today.Year - MinAge;
            var years = new List<int>(YearSpan + 1);
            for (int i = 0; i <= YearSpan; i++)
            {
                years.Add(latest - i);
            }

            return years;
        }
    }
}
