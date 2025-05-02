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

                // Get all available achievements (consider caching this if it becomes large)
                var allAchievements = await _context.Achievements.ToListAsync();

                // Get user's mystery progress
                var userMysteries = await _context.UserMysteries
                    .Where(um => um.UserId == userId)
                    .Include(um => um.Mystery) // Include Mystery for difficulty/category checks
                    .ToListAsync();

                // Get user's created mysteries
                var createdMysteryIds = await _context.Mysteries
                                            .Where(m => m.CreatorId == userId)
                                            .Select(m => m.Id)
                                            .ToListAsync();
                var createdMysteriesCount = createdMysteryIds.Count;


                // --- Calculate Stats ---
                int solvedCount = userMysteries.Count(um => um.IsCompleted);
                int attemptedDifferentCount = userMysteries.Select(um => um.MysteryId).Distinct().Count();
                int totalAttempts = userMysteries.Sum(um => um.AttemptsCount);

                int beginnerSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Difficulty == "Beginner");
                int intermediateSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Difficulty == "Intermediate");
                int advancedSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Difficulty == "Advanced");
                int expertSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Difficulty == "Expert");

                int dataRecoverySolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "Data Recovery");
                int businessSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "Business Analytics");
                int securitySolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "Security Audit");
                int puzzleSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "General Puzzle");
                int algorithmSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "Algorithm Challenge");

                bool solvedFirstAttempt = userMysteries.Any(um => um.IsCompleted && um.AttemptsCount == 1);
                int solvedFirstAttemptCount = userMysteries.Count(um => um.IsCompleted && um.AttemptsCount == 1);

                bool solvedBeginnerUnder5 = userMysteries.Any(um => um.IsCompleted && um.Mystery.Difficulty == "Beginner" && um.AttemptsCount <= 5);
                bool solvedIntermediateUnder10 = userMysteries.Any(um => um.IsCompleted && um.Mystery.Difficulty == "Intermediate" && um.AttemptsCount <= 10);
                bool solvedAdvancedUnder15 = userMysteries.Any(um => um.IsCompleted && um.Mystery.Difficulty == "Advanced" && um.AttemptsCount <= 15);
                bool solvedExpertUnder20 = userMysteries.Any(um => um.IsCompleted && um.Mystery.Difficulty == "Expert" && um.AttemptsCount <= 20);

                bool over100AttemptsOneMystery = userMysteries.Any(um => um.AttemptsCount > 100);

                // Check if at least one mystery solved in each category/difficulty
                var solvedCategories = userMysteries.Where(um => um.IsCompleted).Select(um => um.Mystery.Category).Distinct().ToList();
                var allCategories = await _context.Mysteries.Select(m => m.Category).Distinct().ToListAsync(); // Get all possible categories
                bool solvedEachCategory = allCategories.Any() && allCategories.All(cat => solvedCategories.Contains(cat)); // Ensure categories exist

                var solvedDifficulties = userMysteries.Where(um => um.IsCompleted).Select(um => um.Mystery.Difficulty).Distinct().ToList();
                var allDifficulties = await _context.Mysteries.Select(m => m.Difficulty).Distinct().ToListAsync(); // Get all possible difficulties
                bool solvedEachDifficulty = allDifficulties.Any() && allDifficulties.All(diff => solvedDifficulties.Contains(diff)); // Ensure difficulties exist

                // --- Calculate Creator Stats (Solves by Others) ---
                int maxSolvesByOthers = 0;
                if (createdMysteryIds.Any())
                {
                    var solvesByOthers = await _context.UserMysteries
                        .Where(um => createdMysteryIds.Contains(um.MysteryId) // Mystery created by this user
                                     && um.IsCompleted                     // It was solved
                                     && um.UserId != userId)                // By someone else
                        .GroupBy(um => um.MysteryId)
                        .Select(g => new { MysteryId = g.Key, Count = g.Count() })
                        .ToListAsync();

                    if (solvesByOthers.Any())
                    {
                        maxSolvesByOthers = solvesByOthers.Max(s => s.Count);
                    }
                }


                // --- Check Achievements ---

                // Helper function to reduce repetition
                async Task CheckAndAward(string criteria, bool condition)
                {
                    var achievement = allAchievements.FirstOrDefault(a => a.Criteria == criteria);
                    if (achievement != null && !userAchievements.Contains(achievement.Id) && condition)
                    {
                        var userAchievement = await AwardAchievementAsync(userId, achievement.Id);
                        if (userAchievement != null) newAchievements.Add(userAchievement);
                    }
                }

                // Solving Milestones
                await CheckAndAward("FirstMystery", solvedCount >= 1);
                await CheckAndAward("Solve5Mysteries", solvedCount >= 5);
                await CheckAndAward("Solve10Mysteries", solvedCount >= 10);
                await CheckAndAward("Solve25Mysteries", solvedCount >= 25);
                await CheckAndAward("Solve50Mysteries", solvedCount >= 50);
                await CheckAndAward("Solve100Mysteries", solvedCount >= 100);
                await CheckAndAward("Solve200Mysteries", solvedCount >= 200);

                // Difficulty Milestones
                await CheckAndAward("Solve3Beginner", beginnerSolvedCount >= 3);
                await CheckAndAward("Solve3Intermediate", intermediateSolvedCount >= 3);
                await CheckAndAward("Solve3Advanced", advancedSolvedCount >= 3);
                await CheckAndAward("Solve1Expert", expertSolvedCount >= 1);
                await CheckAndAward("Solve3Expert", expertSolvedCount >= 3);
                await CheckAndAward("Solve10Intermediate", intermediateSolvedCount >= 10);
                await CheckAndAward("Solve10Advanced", advancedSolvedCount >= 10);
                await CheckAndAward("Solve10Expert", expertSolvedCount >= 10);

                // Query Writing Milestones
                await CheckAndAward("Write10Queries", totalAttempts >= 10);
                await CheckAndAward("Write50Queries", totalAttempts >= 50);
                await CheckAndAward("Write100Queries", totalAttempts >= 100);
                await CheckAndAward("Write250Queries", totalAttempts >= 250);
                await CheckAndAward("Write500Queries", totalAttempts >= 500);
                await CheckAndAward("Write750Queries", totalAttempts >= 750);
                await CheckAndAward("Write1000Queries", totalAttempts >= 1000);
                await CheckAndAward("Write1500Queries", totalAttempts >= 1500);

                // Creation Milestones
                await CheckAndAward("CreateFirstMystery", createdMysteriesCount >= 1);
                await CheckAndAward("Create5Mysteries", createdMysteriesCount >= 5);
                await CheckAndAward("Create10Mysteries", createdMysteriesCount >= 10);
                await CheckAndAward("Create15Mysteries", createdMysteriesCount >= 15);
                await CheckAndAward("Create25Mysteries", createdMysteriesCount >= 25);

                // Category Milestones
                await CheckAndAward("Solve5DataRecovery", dataRecoverySolvedCount >= 5);
                await CheckAndAward("Solve5Business", businessSolvedCount >= 5);
                await CheckAndAward("Solve5Security", securitySolvedCount >= 5);
                await CheckAndAward("Solve5Puzzle", puzzleSolvedCount >= 5);
                await CheckAndAward("Solve5Algorithm", algorithmSolvedCount >= 5);

                // Attempt Milestones
                await CheckAndAward("Attempt10Mysteries", attemptedDifferentCount >= 10);
                await CheckAndAward("Attempt25Mysteries", attemptedDifferentCount >= 25);
                await CheckAndAward("Attempt50Mysteries", attemptedDifferentCount >= 50);

                // Skill/Efficiency Milestones
                await CheckAndAward("SolveBeginnerUnder5", solvedBeginnerUnder5);
                await CheckAndAward("SolveIntermediateUnder10", solvedIntermediateUnder10);
                await CheckAndAward("SolveAdvancedUnder15", solvedAdvancedUnder15);
                await CheckAndAward("SolveExpertUnder20", solvedExpertUnder20);
                await CheckAndAward("SolveFirstAttempt", solvedFirstAttempt);
                await CheckAndAward("Solve5FirstAttempt", solvedFirstAttemptCount >= 5);

                // Other Milestones
                await CheckAndAward("Over100AttemptsOneMystery", over100AttemptsOneMystery);
                await CheckAndAward("SolveEachCategory", solvedEachCategory);
                await CheckAndAward("SolveEachDifficulty", solvedEachDifficulty);

                // Creator Popularity Milestones
                await CheckAndAward("MysterySolved10Times", maxSolvesByOthers >= 10);
                await CheckAndAward("MysterySolved50Times", maxSolvesByOthers >= 50);

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
                // Double-check if already awarded just before adding
                bool alreadyAwarded = await _context.UserAchievements
                                            .AnyAsync(ua => ua.UserId == userId && ua.AchievementId == achievementId);
                if (alreadyAwarded)
                {
                    _logger.LogWarning("Attempted to re-award achievement {AchievementId} to user {UserId}", achievementId, userId);
                    return null;
                }

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

                // Eager load the Achievement details for the notification
                await _context.Entry(userAchievement)
                              .Reference(ua => ua.Achievement)
                              .LoadAsync();

                return userAchievement;
            }
            catch (DbUpdateException dbEx) // Catch specific DB exceptions, e.g., potential unique constraint violation if check fails
            {
                 _logger.LogError(dbEx, "Database error awarding achievement {AchievementId} to user {UserId}. Might be a concurrency issue.", achievementId, userId);
                 return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "General error awarding achievement {AchievementId} to user {UserId}", achievementId, userId);
                return null;
            }
        }

        public async Task<List<UserAchievement>> GetPendingAchievementNotificationsAsync(string userId)
        {
            try
            {
                return await _context.UserAchievements
                    .Include(ua => ua.Achievement) // Eager load Achievement details
                    .Where(ua => ua.UserId == userId && !ua.NotificationDisplayed)
                    .OrderBy(ua => ua.EarnedOn) // Show oldest first
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
            if (userAchievementIds == null || !userAchievementIds.Any())
            {
                return; // Nothing to mark
            }

            try
            {
                // Efficiently update in batches using ExecuteUpdateAsync (EF Core 7+)
                // For older EF Core versions, use the fetch and loop method below.
                 await _context.UserAchievements
                     .Where(ua => userAchievementIds.Contains(ua.Id) && !ua.NotificationDisplayed)
                     .ExecuteUpdateAsync(setters => setters.SetProperty(ua => ua.NotificationDisplayed, true));

                // Fallback for older EF Core versions:
                /*
                var notifications = await _context.UserAchievements
                    .Where(ua => userAchievementIds.Contains(ua.Id))
                    .ToListAsync();

                if (notifications.Any())
                {
                    foreach (var notification in notifications)
                    {
                        notification.NotificationDisplayed = true;
                    }
                    await _context.SaveChangesAsync();
                }
                */
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking achievement notifications as displayed for IDs: {UserAchievementIds}", string.Join(",", userAchievementIds));
            }
        }
    }
}