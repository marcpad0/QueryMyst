using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QueryMyst.Data;
using QueryMyst.Models;
using QueryMyst.Services;

namespace QueryMyst.Pages.API
{
    [Route("api/achievements")]
    [ApiController]
    [Authorize]
    public class AchievementsController : ControllerBase
    {
        private readonly AchievementService _achievementService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AchievementsController(
            AchievementService achievementService,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context)
        {
            _achievementService = achievementService;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<object>>> GetPendingNotifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            
            // Get pending notifications
            var pendingNotifications = await _achievementService.GetPendingAchievementNotificationsAsync(user.Id);
            
            // Map to anonymous objects with only needed properties
            var result = pendingNotifications.Select(ua => new {
                id = ua.Id,
                name = ua.Achievement.Name,
                description = ua.Achievement.Description,
                icon = ua.Achievement.Icon,
                points = ua.Achievement.PointsValue
            });
            
            return Ok(result);
        }
        
        [HttpPost("mark-displayed")]
        public async Task<ActionResult> MarkNotificationsAsDisplayed([FromBody] MarkDisplayedRequest request)
        {
            if (request?.AchievementIds == null || !request.AchievementIds.Any())
            {
                return BadRequest("No achievement IDs provided");
            }
            
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }
            
            // Verify ownership of achievements
            var userAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == user.Id && request.AchievementIds.Contains(ua.Id))
                .ToListAsync();
                
            if (userAchievements.Count != request.AchievementIds.Count)
            {
                return BadRequest("Some achievement IDs are invalid or don't belong to the current user");
            }
            
            // Mark as displayed
            await _achievementService.MarkNotificationsAsDisplayedAsync(request.AchievementIds);
            
            return Ok();
        }
        
        public class MarkDisplayedRequest
        {
            public List<int> AchievementIds { get; set; }
        }
    }
}