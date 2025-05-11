using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QueryMyst.Data;
using QueryMyst.Models; // Assuming a LeaderboardEntry model/viewmodel might be created later
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QueryMyst.Pages
{
    [Authorize] // Or remove if leaderboard should be public
    public class LeaderboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<LeaderboardModel> _logger;

        public LeaderboardModel(ApplicationDbContext context, UserManager<IdentityUser> userManager, ILogger<LeaderboardModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // Simple class to hold leaderboard data
        public class LeaderboardEntry
        {
            public int Rank { get; set; }
            public string UserName { get; set; }
            public int SolvedCount { get; set; }
            public bool IsCurrentUser { get; set; }
            // Add other metrics like AverageAttempts, FastestCompletion etc. later if needed
        }

        public List<LeaderboardEntry> Leaderboard { get; set; } = new List<LeaderboardEntry>();
        public string CurrentUserId { get; set; }
        public int CurrentUserRank { get; set; }
        public bool CurrentUserInTop100 { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                // Should not happen if [Authorize] is used, but good practice
                return Challenge();
            }
            CurrentUserId = currentUser.Id;

            _logger.LogInformation("Fetching leaderboard data.");

            try
            {
                // 1. Get completed mysteries grouped by user
                var userScores = await _context.UserMysteries
                    .Where(um => um.IsCompleted)
                    .GroupBy(um => um.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        SolvedCount = g.Count()
                        // Could add other metrics here, e.g., g.Sum(um => um.AttemptsCount)
                    })
                    .OrderByDescending(u => u.SolvedCount)
                    // Add .ThenBy() for tie-breaking if needed (e.g., completion date)
                    .ToListAsync();

                // 2. Get Usernames for all users in the leaderboard
                var userIds = userScores.Select(u => u.UserId).ToList();
                var users = await _context.Users
                                          .Where(u => userIds.Contains(u.Id))
                                          .ToDictionaryAsync(u => u.Id, u => u.UserName);

                // 3. Find current user's position in the full leaderboard
                var currentUserScore = userScores.FirstOrDefault(u => u.UserId == currentUser.Id);
                CurrentUserRank = currentUserScore != null 
                    ? userScores.FindIndex(u => u.UserId == currentUser.Id) + 1 
                    : userScores.Count + 1; // If user has no solved mysteries, they're last

                // 4. Take only top 100 for display
                var top100 = userScores.Take(100).ToList();
                
                // 5. Check if current user is in top 100
                CurrentUserInTop100 = top100.Any(u => u.UserId == currentUser.Id);

                // 6. Combine data and assign ranks for top 100
                Leaderboard = top100.Select((score, index) => new LeaderboardEntry
                {
                    Rank = index + 1,
                    UserName = users.TryGetValue(score.UserId, out var userName) ? userName : "Unknown User",
                    SolvedCount = score.SolvedCount,
                    IsCurrentUser = score.UserId == currentUser.Id
                }).ToList();

                // 7. If current user isn't in top 100, add them to the list with their actual rank
                if (!CurrentUserInTop100 && currentUserScore != null)
                {
                    Leaderboard.Add(new LeaderboardEntry
                    {
                        Rank = CurrentUserRank,
                        UserName = currentUser.UserName,
                        SolvedCount = currentUserScore.SolvedCount,
                        IsCurrentUser = true
                    });
                }

                _logger.LogInformation("Successfully generated leaderboard with top 100 entries. Current user rank: {Rank}", CurrentUserRank);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating leaderboard.");
                // Optionally add an error message to display on the page
                // TempData["ErrorMessage"] = "Could not load leaderboard data.";
            }

            return Page();
        }
    }
}