using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QueryMyst.Data;
using QueryMyst.Models;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Identity; // Required for UserManager
using Microsoft.Extensions.Logging; // Required for ILogger
using System.Collections.Generic; // Required for List
using System; // Required for StringSplitOptions
using Microsoft.EntityFrameworkCore; // Required for DbUpdateException

namespace QueryMyst.Pages.Mysteries
{
    [Authorize] // Ensure only logged-in users can create mysteries
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CreateModel> _logger;
        private readonly UserManager<IdentityUser> _userManager; // Inject UserManager

        public CreateModel(ApplicationDbContext context, ILogger<CreateModel> logger, UserManager<IdentityUser> userManager) // Add UserManager to constructor
        {
            _context = context;
            _logger = logger;
            _userManager = userManager; // Assign UserManager
        }

        [BindProperty]
        public InputModel Input { get; set; }

        // Define an InputModel to encapsulate all form fields for better organization and validation
        public class InputModel
        {
            [Required(ErrorMessage = "Mystery title is required.")]
            [MaxLength(100)]
            public string Title { get; set; }

            [MaxLength(500, ErrorMessage = "Short description cannot exceed 500 characters.")]
            [Display(Name = "Short Description")]
            public string Description { get; set; } // Short description

            [Required(ErrorMessage = "Please select a difficulty level.")]
            [MaxLength(20)]
            public string Difficulty { get; set; } = "Beginner"; // Default value

            [Required(ErrorMessage = "Please select a category.")]
            [MaxLength(50)]
            public string Category { get; set; }

            [MaxLength(100, ErrorMessage = "Icon HTML is too long (max 100 chars).")]
            [Display(Name = "Icon HTML (e.g., <i class='bi bi-icon'></i>)")]
            public string Icon { get; set; } = "<i class='bi bi-question-diamond fs-1 text-secondary'></i>"; // Default icon

            [Display(Name = "Required Skills (comma-separated)")]
            public string RequiredSkillsInput { get; set; }

            [Required(ErrorMessage = "Full description is required.")]
            [Display(Name = "Full Description / Scenario")]
            [DataType(DataType.MultilineText)]
            public string FullDescription { get; set; }

            [Required(ErrorMessage = "Database schema definition (SQL CREATE statements) is required.")]
            [Display(Name = "Database Schema (SQL)")]
            [DataType(DataType.MultilineText)]
            public string DatabaseSchema { get; set; }

            [Required(ErrorMessage = "Sample data script (SQL INSERT statements) is required.")]
            [Display(Name = "Sample Data (SQL)")]
            [DataType(DataType.MultilineText)]
            public string SampleData { get; set; }

            [Required(ErrorMessage = "The solution query (SQL SELECT statement) is required.")]
            [Display(Name = "Solution Query (SQL)")]
            [DataType(DataType.MultilineText)]
            public string SolutionQuery { get; set; }

            [Display(Name = "Hint Text (Optional)")]
            [DataType(DataType.MultilineText)]
            public string HintText { get; set; }

            [Display(Name = "False Clues / Distractors (Optional)")]
            [DataType(DataType.MultilineText)]
            public string FalseClues { get; set; }
        }

        // Predefined options for dropdowns - could also be loaded from DB if needed
        public List<string> DifficultyOptions { get; } = new List<string> { "Beginner", "Intermediate", "Advanced", "Expert" };
        public List<string> CategoryOptions { get; } = new List<string> { "Business Analytics", "Data Recovery", "Security Audit", "General Puzzle", "Algorithm Challenge" };

        public IActionResult OnGet()
        {
            // Initialize InputModel with defaults
            Input = new InputModel();
            _logger.LogInformation("Create Mystery page accessed by user: {User}", User.Identity?.Name ?? "Anonymous");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Re-populate dropdown options in case of validation failure and page redisplay
            // (Not strictly necessary here as they are hardcoded, but good practice if loaded dynamically)

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid during mystery creation attempt by user: {User}", User.Identity?.Name);
                // Log specific validation errors
                foreach (var state in ModelState)
                {
                    if (state.Value.Errors.Any())
                    {
                        _logger.LogWarning("Validation Error for {Field}: {ErrorMessage}", state.Key, state.Value.Errors.First().ErrorMessage);
                    }
                }
                return Page(); // Return the page with validation errors displayed
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                // This should not happen if [Authorize] is effective, but handle defensively
                _logger.LogError("Authenticated user could not be found during mystery creation POST request.");
                ModelState.AddModelError(string.Empty, "Unable to identify the current user. Please log in again.");
                return Page();
            }

            // Map Difficulty to DifficultyClass (e.g., "Intermediate" -> "difficulty-intermediate")
            string difficultyClass = $"difficulty-{Input.Difficulty?.ToLowerInvariant().Replace(" ", "-") ?? "beginner"}";

            // Parse comma-separated skills, trimming whitespace and removing empty entries, ensure uniqueness
            var skillsList = string.IsNullOrWhiteSpace(Input.RequiredSkillsInput)
                ? new List<string>()
                : Input.RequiredSkillsInput
                       .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Select(s => s.Trim()) // Ensure trimming again
                       .Where(s => !string.IsNullOrEmpty(s)) // Ensure no empty strings after trimming
                       .Distinct(StringComparer.OrdinalIgnoreCase) // Case-insensitive distinct skills
                       .ToList();

            // Create the main Mystery entity
            var mystery = new Mystery
            {
                Title = Input.Title.Trim(), // Trim input strings
                Description = Input.Description?.Trim(),
                Difficulty = Input.Difficulty,
                DifficultyClass = difficultyClass,
                Category = Input.Category,
                Icon = Input.Icon?.Trim(), // Use provided or default icon
                RequiredSkills = skillsList, // Assign parsed list
                CreatorId = currentUser.Id, // Set the CreatorId to the current user
                UserMysteries = new List<UserMystery>() // Initialize collection
                // Details are created and linked below
            };

            // Create the associated MysteryDetails entity
            var mysteryDetails = new MysteryDetails
            {
                FullDescription = Input.FullDescription.Trim(),
                DatabaseSchema = Input.DatabaseSchema.Trim(),
                SampleData = Input.SampleData.Trim(),
                SolutionQuery = Input.SolutionQuery.Trim(),
                HintText = Input.HintText?.Trim(),
                FalseClues = Input.FalseClues?.Trim(),
                Mystery = mystery // Link back to the parent Mystery object (EF Core handles FK)
            };

            // Assign the details to the mystery (establishes the one-to-one relationship)
            mystery.Details = mysteryDetails;

            try
            {
                // Add the Mystery (EF Core will also add the related MysteryDetails)
                _context.Mysteries.Add(mystery);
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {UserId} ('{UserName}') successfully created new mystery '{MysteryTitle}' with ID {MysteryId}.",
                    currentUser.Id, currentUser.UserName, mystery.Title, mystery.Id);

                // Optionally: Add a success message to TempData to display on the redirected page
                TempData["SuccessMessage"] = $"Mystery '{mystery.Title}' created successfully!";

                // Redirect to the list page after successful creation
                return RedirectToPage("/Mysteries"); // Corrected path: Use absolute path from Pages root
            }
            catch (DbUpdateException dbEx) // Catch specific DB errors
            {
                 // Log the inner exception if available, as it often contains more details
                 _logger.LogError(dbEx, "Database error saving new mystery '{MysteryTitle}' created by user {UserId}. Inner Exception: {InnerException}",
                     Input.Title, currentUser.Id, dbEx.InnerException?.Message);
                 ModelState.AddModelError(string.Empty, $"A database error occurred: {dbEx.InnerException?.Message ?? dbEx.Message}. Please check the data and try again.");
                 return Page(); // Return with error message
            }
            catch (Exception ex) // Catch unexpected errors
            {
                _logger.LogError(ex, "Unexpected error saving new mystery '{MysteryTitle}' created by user {UserId}.", Input.Title, currentUser.Id);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred while saving the mystery. Please try again later.");
                return Page(); // Return with error message
            }
        }
    }
}