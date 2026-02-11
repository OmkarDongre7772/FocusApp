using System.Collections.ObjectModel;
using FocusTracker.UI.Models;

namespace FocusTracker.UI
{
    public class DashboardViewModel
    {
        private readonly DashboardService _service;

        public ObservableCollection<DailySummaryModel> DailySummaries { get; }
        public ObservableCollection<FocusSessionModel> RecentSessions { get; }

        public DashboardViewModel()
        {
            _service = new DashboardService();

            DailySummaries =
                new ObservableCollection<DailySummaryModel>(_service.GetDailySummaries());

            RecentSessions =
                new ObservableCollection<FocusSessionModel>(_service.GetRecentSessions());
        }
    }
}
