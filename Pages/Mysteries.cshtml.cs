using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QueryMyst.Data;
using QueryMyst.Models;

namespace QueryMyst.Pages
{
    [Authorize]
    public class MysteriesModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<DashboardModel> _logger;
        private readonly ApplicationDbContext _context;

        public List<Mystery> Mysteries { get; set; }
        public Dictionary<int, bool> UserCompletedMysteries { get; set; } = new();
        public Dictionary<int, int> MysterySolvedCounts { get; set; } = new();

        public MysteriesModel(
            UserManager<IdentityUser> userManager,
            ILogger<DashboardModel> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }
            
            // Get all mysteries and their completion status for the current user
            Mysteries = await _context.Mysteries
                .Include(m => m.UserMysteries)
                .ToListAsync();
                
            // Get user's completed mysteries
            var userMysteries = await _context.UserMysteries
                .Where(um => um.UserId == user.Id)
                .ToDictionaryAsync(um => um.MysteryId, um => um.IsCompleted);
                
            // Populate completion status
            UserCompletedMysteries = userMysteries;
            
            // Calculate solved counts for each mystery
            MysterySolvedCounts = Mysteries.ToDictionary(
                m => m.Id, 
                m => m.UserMysteries.Count(um => um.IsCompleted)
            );
            
            _logger.LogInformation("User accessed mysteries page: {UserName}", user.UserName);
            return Page();
        }
    }
}