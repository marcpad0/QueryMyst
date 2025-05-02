using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QueryMyst.Data;
using QueryMyst.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QueryMyst.Pages
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<DashboardModel> _logger;
        private readonly ApplicationDbContext _context;

        public DashboardModel(
            UserManager<IdentityUser> userManager, 
            ILogger<DashboardModel> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        public int MysteriesSolved { get; set; }
        public int TotalQueriesWritten { get; set; }
        public int TotalBadges { get; set; }
        public int ProgressPercentage { get; set; }
        public List<UserAchievementViewModel> RecentAchievements { get; set; } = new List<UserAchievementViewModel>();
        public Mystery FeaturedMystery { get; set; }
        public List<LeaderboardEntryViewModel> TopLeaderboard { get; set; } = new List<LeaderboardEntryViewModel>();
        public int UserRank { get; set; }
        public int UserScore { get; set; }

        public class UserAchievementViewModel
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Icon { get; set; }
            public int Points { get; set; }
            public DateTime EarnedOn { get; set; }
            public TimeSpan TimeAgo => DateTime.Now - EarnedOn;
            public string FormattedTimeAgo 
            {
                get
                {
                    if (TimeAgo.TotalDays > 365)
                        return $"{(int)(TimeAgo.TotalDays / 365)} year{((int)(TimeAgo.TotalDays / 365) != 1 ? "s" : "")} ago";
                    if (TimeAgo.TotalDays > 30)
                        return $"{(int)(TimeAgo.TotalDays / 30)} month{((int)(TimeAgo.TotalDays / 30) != 1 ? "s" : "")} ago";
                    if (TimeAgo.TotalDays >= 1)
                        return $"{(int)TimeAgo.TotalDays} day{((int)TimeAgo.TotalDays != 1 ? "s" : "")} ago";
                    if (TimeAgo.TotalHours >= 1)
                        return $"{(int)TimeAgo.TotalHours} hour{((int)TimeAgo.TotalHours != 1 ? "s" : "")} ago";
                    if (TimeAgo.TotalMinutes >= 1)
                        return $"{(int)TimeAgo.TotalMinutes} minute{((int)TimeAgo.TotalMinutes != 1 ? "s" : "")} ago";
                    return "just now";
                } 
            }
        }

        public class LeaderboardEntryViewModel
        {
            public int Rank { get; set; }
            public string UserName { get; set; }
            public int Score { get; set; }
            public int SolvedMysteries { get; set; }
            public bool IsCurrentUser { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            _logger.LogInformation("User accessed dashboard: {UserName}", user.UserName);

            // Get user's solved mysteries
            var userMysteries = await _context.UserMysteries
                .Where(um => um.UserId == user.Id)
                .ToListAsync();

            MysteriesSolved = userMysteries.Count(um => um.IsCompleted);
            TotalQueriesWritten = userMysteries.Sum(um => um.AttemptsCount);

            // Get user's achievements
            var userAchievements = await _context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == user.Id)
                .OrderByDescending(ua => ua.EarnedOn)
                .ToListAsync();

            TotalBadges = userAchievements.Count;

            // Calculate progress percentage based on total mysteries solved out of all available
            var totalMysteries = await _context.Mysteries.CountAsync();
            ProgressPercentage = totalMysteries > 0 ? (int)((double)MysteriesSolved / totalMysteries * 100) : 0;

            // Get recent achievements (up to 3)
            RecentAchievements = userAchievements
                .Take(3)
                .Select(ua => new UserAchievementViewModel
                {
                    Id = ua.AchievementId,
                    Name = ua.Achievement.Name,
                    Description = ua.Achievement.Description,
                    Icon = ua.Achievement.Icon,
                    Points = ua.Achievement.PointsValue,
                    EarnedOn = ua.EarnedOn
                })
                .ToList();

            // FIX: Get featured mystery by first retrieving all mysteries and then randomizing in memory
            // Step 1: Get all mysteries with their details (without random ordering in the query)
            var allMysteries = await _context.Mysteries
                .Include(m => m.Details)
                .ToListAsync();

            // Step 2: Randomize the order in memory after fetching from database
            allMysteries = allMysteries.OrderBy(_ => Guid.NewGuid()).ToList();

            // Step 3: Create a set of IDs for mysteries that the user has completed
            var completedMysteryIds = userMysteries
                .Where(um => um.IsCompleted)
                .Select(um => um.MysteryId)
                .ToHashSet();

            // Step 4: Find the first mystery that hasn't been completed
            FeaturedMystery = allMysteries
                .FirstOrDefault(m => !completedMysteryIds.Contains(m.Id));

            // If no uncompleted mysteries found, get any random mystery
            if (FeaturedMystery == null && allMysteries.Any())
            {
                FeaturedMystery = allMysteries.First();
            }

            // Get leaderboard
            // Calculate scores (solved mysteries count * 100 + total achievements * 10)
            var userScores = await _context.Users
                .Select(u => new
                {
                    UserId = u.Id,
                    UserName = u.UserName,
                    SolvedCount = _context.UserMysteries
                        .Count(um => um.UserId == u.Id && um.IsCompleted),
                    AchievementsCount = _context.UserAchievements
                        .Count(ua => ua.UserId == u.Id)
                })
                .ToListAsync();

            // Calculate score for each user
            var scoredUsers = userScores
                .Select(u => new
                {
                    u.UserId,
                    u.UserName,
                    u.SolvedCount,
                    Score = (u.SolvedCount * 100) + (u.AchievementsCount * 10)
                })
                .OrderByDescending(u => u.Score)
                .ToList();

            // Find current user's rank
            var currentUserEntry = scoredUsers.FirstOrDefault(u => u.UserId == user.Id);
            UserRank = scoredUsers.FindIndex(u => u.UserId == user.Id) + 1;
            UserScore = currentUserEntry?.Score ?? 0;

            // Take top 5 users and the user themselves for the leaderboard
            TopLeaderboard = scoredUsers
                .Take(5)  // Top 5 users instead of 2
                .Select((u, index) => new LeaderboardEntryViewModel
                {
                    Rank = index + 1,
                    UserName = u.UserName,
                    Score = u.Score,
                    SolvedMysteries = u.SolvedCount,
                    IsCurrentUser = u.UserId == user.Id
                })
                .ToList();

            // Add current user to leaderboard if not already there
            if (!TopLeaderboard.Any(l => l.IsCurrentUser) && currentUserEntry != null)
            {
                TopLeaderboard.Add(new LeaderboardEntryViewModel
                {
                    Rank = UserRank,
                    UserName = currentUserEntry.UserName,
                    Score = UserScore,
                    SolvedMysteries = currentUserEntry.SolvedCount,
                    IsCurrentUser = true
                });
            }

            return Page();
        }
    }
}