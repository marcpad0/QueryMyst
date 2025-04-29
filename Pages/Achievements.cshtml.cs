using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QueryMyst.Data;
using QueryMyst.Models;
using QueryMyst.Services;

namespace QueryMyst.Pages
{
    [Authorize]
    public class AchievementsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<AchievementsModel> _logger;
        private readonly AchievementService _achievementService;

        public AchievementsModel(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<AchievementsModel> logger,
            AchievementService achievementService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _achievementService = achievementService;
        }
        
        public class AchievementViewModel
        {
            public Achievement Achievement { get; set; }
            public bool IsEarned { get; set; }
            public DateTime? EarnedOn { get; set; }
            // Progress info for some achievements
            public int CurrentProgress { get; set; }
            public int RequiredProgress { get; set; }
        }
        
        public List<AchievementViewModel> Achievements { get; set; } = new();
        public int TotalEarned { get; set; }
        public int TotalPoints { get; set; }
        public int TotalAchievements { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge(); // Redirect to login if user is not authenticated
            }
            
            // Get all achievements
            var allAchievements = await _context.Achievements.ToListAsync();
            TotalAchievements = allAchievements.Count;
            
            // Get user's earned achievements
            var userAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == user.Id)
                .ToListAsync();
                
            TotalEarned = userAchievements.Count;
            
            // Get user's mystery stats for progress calculations
            var userMysteries = await _context.UserMysteries
                .Where(um => um.UserId == user.Id)
                .ToListAsync();
                
            int solvedCount = userMysteries.Count(um => um.IsCompleted);
            int totalAttempts = userMysteries.Sum(um => um.AttemptsCount);
            
            // Map achievements to view models
            Achievements = allAchievements.Select(a => 
            {
                var userAch = userAchievements.FirstOrDefault(ua => ua.AchievementId == a.Id);
                var vm = new AchievementViewModel 
                { 
                    Achievement = a,
                    IsEarned = userAch != null,
                    EarnedOn = userAch?.EarnedOn,
                    CurrentProgress = 0,
                    RequiredProgress = 0
                };
                
                // Calculate progress based on criteria
                switch (a.Criteria)
                {
                    case "FirstMystery":
                        vm.CurrentProgress = solvedCount;
                        vm.RequiredProgress = 1;
                        break;
                    case "Solve5Mysteries":
                        vm.CurrentProgress = solvedCount;
                        vm.RequiredProgress = 5;
                        break;
                    case "Solve10Mysteries":
                        vm.CurrentProgress = solvedCount;
                        vm.RequiredProgress = 10;
                        break;
                    case "Solve25Mysteries":
                        vm.CurrentProgress = solvedCount;
                        vm.RequiredProgress = 25;
                        break;
                    case "Write50Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 50;
                        break;
                    case "Write100Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 100;
                        break;
                    // Additional cases can be added for other achievement types
                }
                
                return vm;
            }).ToList();
            
            // Calculate total points from earned achievements
            TotalPoints = userAchievements
                .Join(allAchievements,
                      ua => ua.AchievementId,
                      a => a.Id,
                      (ua, a) => a.PointsValue)
                .Sum();
                
            // Check for new achievements (this would typically be done when solving mysteries,
            // but we can also check here to ensure the page always shows current progress)
            await _achievementService.CheckAndAwardAchievementsAsync(user.Id);
            
            return Page();
        }
    }
}