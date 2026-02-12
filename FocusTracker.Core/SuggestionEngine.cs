//using System;
//using System.Linq;

//namespace FocusTracker.Core
//{
//    public class SuggestionEngine
//    {
//        private readonly AnalyticsService _analytics = new();

//        public List<Suggestion> GetSuggestions()
//        {
//            var weekly = _analytics.GetLast7Days();
//            var suggestions = new List<Suggestion>();

//            if (weekly.Days.Count < 5)
//                return suggestions; // not enough data

//            var avgFocus = weekly.Days.Average(d => d.FocusMinutes);
//            var avgFrag = weekly.Days.Average(d => d.FragmentationScore);

//            // 🟢 Suggestion 1 — Low focus
//            if (avgFocus < 60)
//            {
//                suggestions.Add(new Suggestion
//                {
//                    Title = "Low focus time",
//                    Message = "Your average focus is below 1 hour/day. Try scheduling one protected focus block.",
//                    Type = SuggestionType.Warning
//                });
//            }

//            // 🟢 Suggestion 2 — High fragmentation
//            if (avgFrag > 60)
//            {
//                suggestions.Add(new Suggestion
//                {
//                    Title = "High context switching",
//                    Message = "You switch apps frequently. Consider batching messages or using Focus Mode.",
//                    Type = SuggestionType.Warning
//                });
//            }

//            // 🟢 Suggestion 3 — Encouragement
//            if (avgFocus > 120 && avgFrag < 30)
//            {
//                suggestions.Add(new Suggestion
//                {
//                    Title = "Great work!",
//                    Message = "You’ve maintained strong focus habits this week. Keep it up 🚀",
//                    Type = SuggestionType.Encouragement
//                });
//            }

//            return suggestions;
//        }
//    }
//}