using System.Collections.Generic;
using System.Linq;

namespace Game.Features.Settings
{
    /// <summary>
    /// FAQ entries of the support center. The wording is the mock-up's placeholder text; replace it (or load it
    /// from the backend) before release.
    /// </summary>
    public static class SupportContent
    {
        public struct Entry
        {
            public int Category;
            public string Question;
            public string Answer;
        }

        /// <summary>Chip labels; index 0 shows every entry.</summary>
        public static readonly string[] Categories = { "All", "Account", "Billing", "Prompting" };

        public static readonly Entry[] Entries =
        {
            new Entry { Category = 3, Question = "Can I edit a generated response?", Answer = "Yes. Tap the answer you want to change, edit the text and send it again to get an updated response." },
            new Entry { Category = 1, Question = "How do I reset my password?", Answer = "You can reset your password by selecting “Forgot password?” on the login screen. Follow the instructions sent to your email to create a new one. If you need further assistance, contact our support team." },
            new Entry { Category = 1, Question = "Can I export my data?", Answer = "Data export is not available yet. Contact our support team and we will send you a copy of your data." },
            new Entry { Category = 1, Question = "How can I delete my history?", Answer = "Open History, swipe a conversation to the left and tap the bin, or use the bin at the top to delete everything." },
            new Entry { Category = 1, Question = "Are my data secure?", Answer = "Your conversations are sent over an encrypted connection and are only visible to your account." },
            new Entry { Category = 2, Question = "How do I report an issue?", Answer = "Open Settings, choose Contact information and send us an email describing the problem." },
        };

        /// <summary>Entries of one category; category 0 ("All") returns everything.</summary>
        public static List<Entry> For(int category)
        {
            return Entries.Where(entry => category <= 0 || entry.Category == category).ToList();
        }
    }
}
