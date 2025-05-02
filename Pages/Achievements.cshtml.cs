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
                .Include(um => um.Mystery) // Include Mystery to access properties like Difficulty and Category
                .ToListAsync();
                
            int solvedCount = userMysteries.Count(um => um.IsCompleted);
            int totalAttempts = userMysteries.Sum(um => um.AttemptsCount);
            
            // Calculate counts for different difficulty levels
            int beginnerSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Difficulty == "Beginner");
            int intermediateSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Difficulty == "Intermediate");
            int advancedSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Difficulty == "Advanced");
            int expertSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Difficulty == "Expert");
            
            // Calculate counts for different categories
            int dataRecoverySolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "Data Recovery");
            int businessSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "Business Analytics");
            int securitySolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "Security Audit");
            int puzzleSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "General Puzzle");
            int algorithmSolvedCount = userMysteries.Count(um => um.IsCompleted && um.Mystery.Category == "Algorithm Challenge");
            
            // Calculate special achievement counts
            int firstAttemptSolvedCount = userMysteries.Count(um => um.IsCompleted && um.AttemptsCount == 1);
            bool solvedBeginnerUnder5 = userMysteries.Any(um => um.IsCompleted && um.Mystery.Difficulty == "Beginner" && um.AttemptsCount <= 5);
            bool solvedIntermediateUnder10 = userMysteries.Any(um => um.IsCompleted && um.Mystery.Difficulty == "Intermediate" && um.AttemptsCount <= 10);
            bool solvedAdvancedUnder15 = userMysteries.Any(um => um.IsCompleted && um.Mystery.Difficulty == "Advanced" && um.AttemptsCount <= 15);
            bool solvedExpertUnder20 = userMysteries.Any(um => um.IsCompleted && um.Mystery.Difficulty == "Expert" && um.AttemptsCount <= 20);
            
            // Get count of mysteries created by the user
            int userCreatedMysteriesCount = await _context.Mysteries
                .CountAsync(m => m.CreatorId == user.Id);

            // Calculate popularity of user's created mysteries (if any)
            int maxSolvesByOthers = 0;
            if (userCreatedMysteriesCount > 0)
            {
                var createdMysteryIds = await _context.Mysteries
                    .Where(m => m.CreatorId == user.Id)
                    .Select(m => m.Id)
                    .ToListAsync();
                    
                var solvesByOthers = await _context.UserMysteries
                    .Where(um => createdMysteryIds.Contains(um.MysteryId) 
                            && um.IsCompleted 
                            && um.UserId != user.Id)
                    .GroupBy(um => um.MysteryId)
                    .Select(g => new { MysteryId = g.Key, Count = g.Count() })
                    .ToListAsync();

                if (solvesByOthers.Any())
                {
                    maxSolvesByOthers = solvesByOthers.Max(s => s.Count);
                }
            }

            // Get all distinct categories and difficulties for achievement tracking
            var solvedCategories = userMysteries.Where(um => um.IsCompleted).Select(um => um.Mystery.Category).Distinct().ToList();
            var allCategories = await _context.Mysteries.Select(m => m.Category).Distinct().ToListAsync();
            bool solvedEachCategory = allCategories.Any() && allCategories.All(cat => solvedCategories.Contains(cat));

            var solvedDifficulties = userMysteries.Where(um => um.IsCompleted).Select(um => um.Mystery.Difficulty).Distinct().ToList();
            var allDifficulties = await _context.Mysteries.Select(m => m.Difficulty).Distinct().ToListAsync();
            bool solvedEachDifficulty = allDifficulties.Any() && allDifficulties.All(diff => solvedDifficulties.Contains(diff));
            
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
                    // Solving count milestones
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
                    case "Solve50Mysteries":
                        vm.CurrentProgress = solvedCount;
                        vm.RequiredProgress = 50;
                        break;
                    case "Solve100Mysteries":
                        vm.CurrentProgress = solvedCount;
                        vm.RequiredProgress = 100;
                        break;
                    case "Solve200Mysteries":
                        vm.CurrentProgress = solvedCount;
                        vm.RequiredProgress = 200;
                        break;
                        
                    // Query writing milestones
                    case "Write50Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 50;
                        break;
                    case "Write100Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 100;
                        break;
                    case "Write500Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 500;
                        break;
                    case "Write10Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 10;
                        break;
                    case "Write250Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 250;
                        break;
                    case "Write750Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 750;
                        break;
                    case "Write1000Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 1000;
                        break;
                    case "Write1500Queries":
                        vm.CurrentProgress = totalAttempts;
                        vm.RequiredProgress = 1500;
                        break;
                        
                    // Difficulty-specific achievements
                    case "Solve3Beginner":
                        vm.CurrentProgress = beginnerSolvedCount;
                        vm.RequiredProgress = 3;
                        break;
                    case "Solve3Intermediate":
                        vm.CurrentProgress = intermediateSolvedCount;
                        vm.RequiredProgress = 3;
                        break;
                    case "Solve10Intermediate":
                        vm.CurrentProgress = intermediateSolvedCount;
                        vm.RequiredProgress = 10;
                        break;
                    case "Solve3Advanced":
                        vm.CurrentProgress = advancedSolvedCount;
                        vm.RequiredProgress = 3;
                        break;
                    case "Solve1Expert":
                        vm.CurrentProgress = expertSolvedCount;
                        vm.RequiredProgress = 1;
                        break;
                    case "Solve3Expert":
                        vm.CurrentProgress = expertSolvedCount;
                        vm.RequiredProgress = 3;
                        break;
                    case "Solve10Advanced":
                        vm.CurrentProgress = advancedSolvedCount;
                        vm.RequiredProgress = 10;
                        break;
                    case "Solve10Expert":
                        vm.CurrentProgress = expertSolvedCount;
                        vm.RequiredProgress = 10;
                        break;
                        
                    // Category-specific achievements
                    case "Solve3DataRecovery":
                        vm.CurrentProgress = dataRecoverySolvedCount;
                        vm.RequiredProgress = 3;
                        break;
                    case "Solve3BusinessAnalytics":
                        vm.CurrentProgress = businessSolvedCount;
                        vm.RequiredProgress = 3;
                        break;
                    case "Solve3SecurityAudit":
                        vm.CurrentProgress = securitySolvedCount;
                        vm.RequiredProgress = 3;
                        break;
                    case "Solve3GeneralPuzzle":
                        vm.CurrentProgress = puzzleSolvedCount;
                        vm.RequiredProgress = 3;
                        break;
                    case "Solve3AlgorithmChallenge":
                        vm.CurrentProgress = algorithmSolvedCount;
                        vm.RequiredProgress = 3;
                        break;
                    case "Solve5DataRecovery":
                        vm.CurrentProgress = dataRecoverySolvedCount;
                        vm.RequiredProgress = 5;
                        break;
                    case "Solve5Business":
                        vm.CurrentProgress = businessSolvedCount;
                        vm.RequiredProgress = 5;
                        break;
                    case "Solve5Security":
                        vm.CurrentProgress = securitySolvedCount;
                        vm.RequiredProgress = 5;
                        break;
                    case "Solve5Puzzle":
                        vm.CurrentProgress = puzzleSolvedCount;
                        vm.RequiredProgress = 5;
                        break;
                    case "Solve5Algorithm":
                        vm.CurrentProgress = algorithmSolvedCount;
                        vm.RequiredProgress = 5;
                        break;
                        
                    // Special solution achievements
                    case "FirstAttemptSolution":
                        vm.CurrentProgress = firstAttemptSolvedCount;
                        vm.RequiredProgress = 1;
                        break;
                    case "FirstAttempt5Times":
                        vm.CurrentProgress = firstAttemptSolvedCount;
                        vm.RequiredProgress = 5;
                        break;
                    case "BeginnerUnder5":
                        vm.CurrentProgress = solvedBeginnerUnder5 ? 1 : 0;
                        vm.RequiredProgress = 1;
                        break;
                    case "IntermediateUnder10":
                        vm.CurrentProgress = solvedIntermediateUnder10 ? 1 : 0;
                        vm.RequiredProgress = 1;
                        break;
                    case "AdvancedUnder15":
                        vm.CurrentProgress = solvedAdvancedUnder15 ? 1 : 0;
                        vm.RequiredProgress = 1;
                        break;
                    case "ExpertUnder20":
                        vm.CurrentProgress = solvedExpertUnder20 ? 1 : 0;
                        vm.RequiredProgress = 1;
                        break;
                    case "SolveAll":
                        vm.CurrentProgress = solvedEachCategory && solvedEachDifficulty ? 1 : 0;
                        vm.RequiredProgress = 1;
                        break;
                    case "SolveAllCategories":
                        vm.CurrentProgress = solvedEachCategory ? 1 : 0;
                        vm.RequiredProgress = 1;
                        break;
                    case "SolveAllDifficulties":
                        vm.CurrentProgress = solvedEachDifficulty ? 1 : 0;
                        vm.RequiredProgress = 1;
                        break;
                    case "PersistentSolver":
                        vm.CurrentProgress = userMysteries.Any(um => um.AttemptsCount > 50) ? 1 : 0;
                        vm.RequiredProgress = 1;
                        break;
                        
                    // Mystery creation achievements
                    case "CreateFirstMystery":
                        vm.CurrentProgress = userCreatedMysteriesCount;
                        vm.RequiredProgress = 1;
                        break;
                    case "Create5Mysteries":
                        vm.CurrentProgress = userCreatedMysteriesCount;
                        vm.RequiredProgress = 5;
                        break;
                    case "Create10Mysteries":
                        vm.CurrentProgress = userCreatedMysteriesCount;
                        vm.RequiredProgress = 10;
                        break;
                    case "Create15Mysteries":
                        vm.CurrentProgress = userCreatedMysteriesCount;
                        vm.RequiredProgress = 15;
                        break;
                    case "Create25Mysteries":
                        vm.CurrentProgress = userCreatedMysteriesCount;
                        vm.RequiredProgress = 25;
                        break;
                        
                    // Mystery popularity achievements
                    case "MysteryPopular":
                        vm.CurrentProgress = maxSolvesByOthers;
                        vm.RequiredProgress = 10;  // Popular if 10+ people solved your mystery
                        break;
                    case "MysteryVeryPopular":
                        vm.CurrentProgress = maxSolvesByOthers;
                        vm.RequiredProgress = 50;  // Very popular if 50+ people solved your mystery
                        break;
                    case "MysterySolved10Times":
                        vm.CurrentProgress = maxSolvesByOthers;
                        vm.RequiredProgress = 10;
                        break;
                    case "MysterySolved50Times":
                        vm.CurrentProgress = maxSolvesByOthers;
                        vm.RequiredProgress = 50;
                        break;
                        
                    // Mystery attempt achievements
                    case "Attempt10Mysteries":
                        vm.CurrentProgress = userMysteries.Select(um => um.MysteryId).Distinct().Count();
                        vm.RequiredProgress = 10;
                        break;
                    case "Attempt25Mysteries":
                        vm.CurrentProgress = userMysteries.Select(um => um.MysteryId).Distinct().Count();
                        vm.RequiredProgress = 25;
                        break;
                    case "Attempt50Mysteries":
                        vm.CurrentProgress = userMysteries.Select(um => um.MysteryId).Distinct().Count();
                        vm.RequiredProgress = 50;
                        break;
                        
                    // First-attempt solution achievements (different naming convention)
                    case "SolveFirstAttempt":
                        vm.CurrentProgress = firstAttemptSolvedCount;
                        vm.RequiredProgress = 1;
                        break;
                    case "Solve5FirstAttempt":
                        vm.CurrentProgress = firstAttemptSolvedCount;
                        vm.RequiredProgress = 5;
                        break;
                        
                    // Persistence achievement
                    case "Over100AttemptsOneMystery":
                        vm.CurrentProgress = userMysteries.Any(um => um.AttemptsCount > 100) ? 1 : 0;
                        vm.RequiredProgress = 1;
                        break;
                        
                    // For any other achievement type that doesn't fit these patterns
                    default:
                        vm.CurrentProgress = 0;
                        vm.RequiredProgress = 1;
                        break;
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