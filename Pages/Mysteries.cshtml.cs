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
        private readonly ILogger<MysteriesModel> _logger;
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

        // Pagination properties
        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; } = 6;
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        // Store distinct values for dropdowns (optional, but good practice)
        public List<string> AvailableDifficulties { get; set; } = new();
        public List<string> AvailableCategories { get; set; } = new();


        public MysteriesModel(
            UserManager<IdentityUser> userManager,
            ILogger<MysteriesModel> logger,
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

            // Base query - Include Creator and UserMysteries
            var query = _context.Mysteries
                                .Include(m => m.Creator)
                                .Include(m => m.UserMysteries)
                                .AsQueryable();

            // Populate filter options before applying filters
            AvailableDifficulties = await _context.Mysteries.Select(m => m.Difficulty).Distinct().OrderBy(d => d).ToListAsync();
            AvailableCategories = await _context.Mysteries.Select(m => m.Category).Distinct().OrderBy(c => c).ToListAsync();

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

            // Get total count for pagination
            TotalItems = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);

            // Ensure page index is within range
            if (PageIndex < 1) PageIndex = 1;
            if (PageIndex > TotalPages && TotalPages > 0) PageIndex = TotalPages;

            // Execute the filtered and paginated query
            Mysteries = await query
                .OrderBy(m => m.Title)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Get user's completed mysteries
            var userMysteries = await _context.UserMysteries
                .Where(um => um.UserId == user.Id)
                .ToDictionaryAsync(um => um.MysteryId, um => um.IsCompleted);

            // Populate completion status for the filtered mysteries
            UserCompletedMysteries = userMysteries;

            // Calculate solved counts for the filtered mysteries
            MysterySolvedCounts = Mysteries.ToDictionary(
                m => m.Id,
                m => m.UserMysteries.Count(um => um.IsCompleted)
            );

            _logger.LogInformation("User {UserName} accessed mysteries page. Page {PageIndex} of {TotalPages}. Filters: Difficulty={Difficulty}, Category={Category}, Search={Search}",
                user.UserName, PageIndex, TotalPages, SelectedDifficulty, SelectedCategory, SearchTerm);

            return Page();
        }
    }
}