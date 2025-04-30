using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QueryMyst.Data;
using QueryMyst.Models;

namespace QueryMyst.Services
{
    public class AchievementService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AchievementService> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public AchievementService(
            ApplicationDbContext context,
            ILogger<AchievementService> logger,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<List<UserAchievement>> CheckAndAwardAchievementsAsync(string userId)
        {
            var newAchievements = new List<UserAchievement>();

            try
            {
                // Get existing user achievements to avoid re-awarding
                var userAchievements = await _context.UserAchievements
                    .Where(ua => ua.UserId == userId)
                    .Select(ua => ua.AchievementId)
                    .ToListAsync();

                // Get all available achievements
                var allAchievements = await _context.Achievements.ToListAsync();

                // Get user's mystery progress
                var userMysteries = await _context.UserMysteries
                    .Where(um => um.UserId == userId)
                    .Include(um => um.Mystery) // Include Mystery for difficulty/category checks if needed later
                    .ToListAsync();

                int solvedCount = userMysteries.Count(um => um.IsCompleted);
                int attemptedCount = userMysteries.Count;
                int totalAttempts = userMysteries.Sum(um => um.AttemptsCount);
                // int beginnerSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Difficulty == "Beginner"); // Needed for Beginner Graduate

                // Check for First Mystery Solved achievement
                var firstSolvedAchievement = allAchievements.FirstOrDefault(a => a.Criteria == "FirstMystery");
                if (firstSolvedAchievement != null &&
                    !userAchievements.Contains(firstSolvedAchievement.Id) &&
                    solvedCount >= 1)
                {
                    var userAchievement = await AwardAchievementAsync(userId, firstSolvedAchievement.Id);
                    if (userAchievement != null) newAchievements.Add(userAchievement);
                }

                // Check for 5 Mysteries Solved achievement
                var fiveSolvedAchievement = allAchievements.FirstOrDefault(a => a.Criteria == "Solve5Mysteries");
                if (fiveSolvedAchievement != null &&
                    !userAchievements.Contains(fiveSolvedAchievement.Id) &&
                    solvedCount >= 5)
                {
                    var userAchievement = await AwardAchievementAsync(userId, fiveSolvedAchievement.Id);
                    if (userAchievement != null) newAchievements.Add(userAchievement);
                }

                // Check for 10 Mysteries Solved achievement
                var tenSolvedAchievement = allAchievements.FirstOrDefault(a => a.Criteria == "Solve10Mysteries");
                if (tenSolvedAchievement != null &&
                    !userAchievements.Contains(tenSolvedAchievement.Id) &&
                    solvedCount >= 10)
                {
                    var userAchievement = await AwardAchievementAsync(userId, tenSolvedAchievement.Id);
                    if (userAchievement != null) newAchievements.Add(userAchievement);
                }

                // Check for 25 Mysteries Solved achievement
                var twentyFiveSolvedAchievement = allAchievements.FirstOrDefault(a => a.Criteria == "Solve25Mysteries");
                if (twentyFiveSolvedAchievement != null &&
                    !userAchievements.Contains(twentyFiveSolvedAchievement.Id) &&
                    solvedCount >= 25)
                {
                    var userAchievement = await AwardAchievementAsync(userId, twentyFiveSolvedAchievement.Id);
                    if (userAchievement != null) newAchievements.Add(userAchievement);
                }

                // Check for 50 Queries written achievement
                var fiftyQueriesAchievement = allAchievements.FirstOrDefault(a => a.Criteria == "Write50Queries");
                if (fiftyQueriesAchievement != null &&
                    !userAchievements.Contains(fiftyQueriesAchievement.Id) &&
                    totalAttempts >= 50)
                {
                    var userAchievement = await AwardAchievementAsync(userId, fiftyQueriesAchievement.Id);
                    if (userAchievement != null) newAchievements.Add(userAchievement);
                }

                // Check for 100 Queries written achievement
                var hundredQueriesAchievement = allAchievements.FirstOrDefault(a => a.Criteria == "Write100Queries");
                if (hundredQueriesAchievement != null &&
                    !userAchievements.Contains(hundredQueriesAchievement.Id) &&
                    totalAttempts >= 100)
                {
                    var userAchievement = await AwardAchievementAsync(userId, hundredQueriesAchievement.Id);
                    if (userAchievement != null) newAchievements.Add(userAchievement);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking or awarding achievements for user {UserId}", userId);
            }

            return newAchievements;
        }
        
        private async Task<UserAchievement> AwardAchievementAsync(string userId, int achievementId)
        {
            try
            {
                var userAchievement = new UserAchievement
                {
                    UserId = userId,
                    AchievementId = achievementId,
                    EarnedOn = DateTime.UtcNow,
                    NotificationDisplayed = false
                };
                
                _context.UserAchievements.Add(userAchievement);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Awarded achievement {AchievementId} to user {UserId}", achievementId, userId);
                
                return userAchievement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error awarding achievement {AchievementId} to user {UserId}", achievementId, userId);
                return null;
            }
        }
        
        public async Task<List<UserAchievement>> GetPendingAchievementNotificationsAsync(string userId)
        {
            try
            {
                return await _context.UserAchievements
                    .Include(ua => ua.Achievement)
                    .Where(ua => ua.UserId == userId && !ua.NotificationDisplayed)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending achievement notifications for user {UserId}", userId);
                return new List<UserAchievement>();
            }
        }
        
        public async Task MarkNotificationsAsDisplayedAsync(List<int> userAchievementIds)
        {
            try
            {
                var notifications = await _context.UserAchievements
                    .Where(ua => userAchievementIds.Contains(ua.Id))
                    .ToListAsync();
                    
                foreach (var notification in notifications)
                {
                    notification.NotificationDisplayed = true;
                }
                
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking achievement notifications as displayed");
            }
        }
    }
}