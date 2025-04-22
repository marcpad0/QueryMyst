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
        private readonly ILogger<DashboardModel> _logger; // Consider changing to ILogger<MysteriesModel>
        private readonly ApplicationDbContext _context;

        public List<Mystery> Mysteries { get; set; }
        public Dictionary<int, bool> UserCompletedMysteries { get; set; } = new();
        public Dictionary<int, int> MysterySolvedCounts { get; set; } = new();

        // Add properties for filtering, bind them from query string
        [BindProperty(SupportsGet = true)]
        public string? SelectedDifficulty { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedCategory { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        // Store distinct values for dropdowns (optional, but good practice)
        public List<string> AvailableDifficulties { get; set; } = new();
        public List<string> AvailableCategories { get; set; } = new();


        public MysteriesModel(
            UserManager<IdentityUser> userManager,
            ILogger<DashboardModel> logger, // Consider changing to ILogger<MysteriesModel>
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

            // Base query
            var query = _context.Mysteries.Include(m => m.UserMysteries).AsQueryable();

            // Populate filter options before applying filters
            AvailableDifficulties = await query.Select(m => m.Difficulty).Distinct().OrderBy(d => d).ToListAsync();
            AvailableCategories = await query.Select(m => m.Category).Distinct().OrderBy(c => c).ToListAsync();


            // Apply filters
            if (!string.IsNullOrEmpty(SelectedDifficulty) && SelectedDifficulty != "All Difficulties")
            {
                query = query.Where(m => m.Difficulty == SelectedDifficulty);
            }

            if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "All Categories")
            {
                query = query.Where(m => m.Category == SelectedCategory);
            }

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                query = query.Where(m => m.Title.Contains(SearchTerm) || m.Description.Contains(SearchTerm));
            }

            // Execute the filtered query
            Mysteries = await query.ToListAsync();

            // Get user's completed mysteries (can be optimized if needed)
            var userMysteries = await _context.UserMysteries
                .Where(um => um.UserId == user.Id)
                .ToDictionaryAsync(um => um.MysteryId, um => um.IsCompleted);

            // Populate completion status for the filtered mysteries
            UserCompletedMysteries = userMysteries;

            // Calculate solved counts for the filtered mysteries
            MysterySolvedCounts = Mysteries.ToDictionary(
                m => m.Id,
                m => m.UserMysteries.Count(um => um.IsCompleted) // UserMysteries are already included
            );

            _logger.LogInformation("User {UserName} accessed mysteries page with filters: Difficulty={Difficulty}, Category={Category}, Search={Search}",
                user.UserName, SelectedDifficulty, SelectedCategory, SearchTerm);

            return Page();
        }
    }
}