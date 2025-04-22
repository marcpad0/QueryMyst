using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QueryMyst.Data;
using QueryMyst.Models;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite; // <-- Add this for SQLite in-memory validation
using System.Text.Json; // <-- Add this (might be needed by helpers)

namespace QueryMyst.Pages.Mysteries
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CreateModel> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateModel(ApplicationDbContext context, ILogger<CreateModel> logger, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Mystery title is required.")]
            [MaxLength(100)]
            public string Title { get; set; }

            [MaxLength(500, ErrorMessage = "Short description cannot exceed 500 characters.")]
            [Display(Name = "Short Description")]
            public string Description { get; set; }

            [Required(ErrorMessage = "Please select a difficulty level.")]
            [MaxLength(20)]
            public string Difficulty { get; set; } = "Beginner";

            [Required(ErrorMessage = "Please select a category.")]
            [MaxLength(50)]
            public string Category { get; set; }

            [MaxLength(100, ErrorMessage = "Icon HTML is too long (max 100 chars).")]
            [Display(Name = "Icon HTML (e.g., <i class='bi bi-icon'></i>)")]
            public string Icon { get; set; } = "<i class='bi bi-question-diamond fs-1 text-secondary'></i>";

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

        public List<string> DifficultyOptions { get; } = new List<string> { "Beginner", "Intermediate", "Advanced", "Expert" };
        public List<string> CategoryOptions { get; } = new List<string> { "Business Analytics", "Data Recovery", "Security Audit", "General Puzzle", "Algorithm Challenge" };

        public IActionResult OnGet()
        {
            Input = new InputModel();
            _logger.LogInformation("Create Mystery page accessed by user: {User}", User.Identity?.Name ?? "Anonymous");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid during mystery creation attempt by user: {User}", User.Identity?.Name);
                LogValidationErrors();
                return Page();
            }

            // --- Start SQL Syntax Validation ---
            bool sqlValid = await ValidateSqlAsync();
            if (!sqlValid)
            {
                 _logger.LogWarning("SQL validation failed for mystery creation attempt by user: {User}", User.Identity?.Name);
                 // Errors are added to ModelState within ValidateSqlAsync
                 return Page(); // Return page with SQL validation errors displayed
            }
            // --- End SQL Syntax Validation ---

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                _logger.LogError("Authenticated user could not be found during mystery creation POST request.");
                ModelState.AddModelError(string.Empty, "Unable to identify the current user. Please log in again.");
                return Page();
            }

            string difficultyClass = $"difficulty-{Input.Difficulty?.ToLowerInvariant().Replace(" ", "-") ?? "beginner"}";
            var skillsList = ParseSkills(Input.RequiredSkillsInput);

            var mystery = new Mystery
            {
                Title = Input.Title.Trim(),
                Description = Input.Description?.Trim(),
                Difficulty = Input.Difficulty,
                DifficultyClass = difficultyClass,
                Category = Input.Category,
                Icon = Input.Icon?.Trim(),
                RequiredSkills = skillsList,
                CreatorId = currentUser.Id,
                UserMysteries = new List<UserMystery>()
            };

            var mysteryDetails = new MysteryDetails
            {
                FullDescription = Input.FullDescription.Trim(),
                DatabaseSchema = Input.DatabaseSchema.Trim(),
                SampleData = Input.SampleData.Trim(),
                SolutionQuery = Input.SolutionQuery.Trim(),
                HintText = Input.HintText?.Trim(),
                FalseClues = Input.FalseClues?.Trim(),
                Mystery = mystery
            };

            mystery.Details = mysteryDetails;

            try
            {
                _context.Mysteries.Add(mystery);
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {UserId} ('{UserName}') successfully created new mystery '{MysteryTitle}' with ID {MysteryId}.",
                    currentUser.Id, currentUser.UserName, mystery.Title, mystery.Id);

                TempData["SuccessMessage"] = $"Mystery '{mystery.Title}' created successfully!";
                return RedirectToPage("./Details", new { id = mystery.Id }); // Redirect to details page
            }
            catch (DbUpdateException dbEx)
            {
                 _logger.LogError(dbEx, "Database error saving new mystery '{MysteryTitle}' created by user {UserId}. Inner Exception: {InnerException}",
                     Input.Title, currentUser.Id, dbEx.InnerException?.Message);
                 ModelState.AddModelError(string.Empty, $"A database error occurred: {dbEx.InnerException?.Message ?? dbEx.Message}. Please check the data and try again.");
                 return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving new mystery '{MysteryTitle}' created by user {UserId}.", Input.Title, currentUser.Id);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred while saving the mystery. Please try again later.");
                return Page();
            }
        }

        // --- Helper Methods ---

        private void LogValidationErrors()
        {
            foreach (var state in ModelState)
            {
                if (state.Value.Errors.Any())
                {
                    _logger.LogWarning("Validation Error for {Field}: {ErrorMessage}", state.Key, state.Value.Errors.First().ErrorMessage);
                }
            }
        }

        private List<string> ParseSkills(string skillsInput)
        {
            return string.IsNullOrWhiteSpace(skillsInput)
                ? new List<string>()
                : skillsInput
                       .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s))
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .ToList();
        }

        // --- SQL Validation Helper ---
        private async Task<bool> ValidateSqlAsync()
        {
            bool isValid = true;
            // Use a unique in-memory database for each validation check
            using (var connection = new SqliteConnection("DataSource=:memory:"))
            {
                try
                {
                    await connection.OpenAsync();

                    // 1. Validate Schema
                    try
                    {
                        // Use helper that handles multi-statement SQL
                        await ExecuteNonQuerySqlAsync(connection, Input.DatabaseSchema);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SQL Schema validation failed.");
                        // Add error to the specific field in the model state
                        ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.DatabaseSchema)}", $"Schema Error: {ex.Message}");
                        isValid = false;
                    }

                    // 2. Validate Sample Data (only if schema was valid)
                    if (isValid)
                    {
                        try
                        {
                            await ExecuteNonQuerySqlAsync(connection, Input.SampleData);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "SQL Sample Data validation failed.");
                            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.SampleData)}", $"Sample Data Error: {ex.Message}");
                            isValid = false;
                        }
                    }

                    // 3. Validate Solution Query (only if schema and data were valid)
                    //    We just check if it executes without error.
                    if (isValid)
                    {
                        try
                        {
                            // Use helper that executes a query and checks for errors
                            var (_, error) = await ExecuteQueryAndCheckErrorAsync(connection, Input.SolutionQuery);
                            if (error != null)
                            {
                                // Error occurred during execution
                                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.SolutionQuery)}", $"Solution Query Error: {error}");
                                isValid = false;
                            }
                            // We don't need the actual result for validation here
                        }
                        catch (Exception ex) // Catch broader exceptions from the helper itself
                        {
                             _logger.LogWarning(ex, "SQL Solution Query validation failed unexpectedly.");
                             ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.SolutionQuery)}", $"Solution Query Error: {ex.Message}");
                             isValid = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Catch potential errors opening the connection itself
                    _logger.LogError(ex, "Failed to open in-memory database for SQL validation.");
                    ModelState.AddModelError(string.Empty, "Could not initialize SQL validation environment.");
                    isValid = false;
                }
                finally
                {
                    // Ensure connection is closed even if errors occurred
                    if (connection.State == System.Data.ConnectionState.Open)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            return isValid;
        }

        // Helper to execute potentially multi-statement non-query SQL (Schema, Inserts)
        private async Task ExecuteNonQuerySqlAsync(SqliteConnection connection, string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return;

            // Simple split by semicolon; might need refinement for complex SQL with semicolons in strings/comments
            // Consider using a more robust SQL parser library for production if needed.
            var commands = sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var commandText in commands)
            {
                var trimmedCommand = commandText.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedCommand))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = trimmedCommand;
                        // This will throw SqliteException on syntax error or constraint violation etc.
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        // Helper to execute a query and return only an error message if it fails
        private async Task<(string resultJson, string error)> ExecuteQueryAndCheckErrorAsync(SqliteConnection connection, string sql)
        {
             if (string.IsNullOrWhiteSpace(sql))
             {
                 return (null, "Query cannot be empty.");
             }
            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    // Execute reader to ensure the query syntax is valid and runs against the schema/data
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // We don't need to read the data for basic validation, just ensure no exception occurs
                        while (await reader.ReadAsync()) { /* Consume results */ }
                    }
                }
                // If execution reached here without exception, the syntax is likely valid
                return ( "{}", null); // Return dummy JSON and no error
            }
            catch (Exception ex) // Catch specific SqliteException or broader Exception
            {
                _logger.LogWarning(ex, "SQL Execution failed during validation for query: {Query}", sql);
                // Return null result and the specific error message
                return (null, ex.Message);
            }
        }
    }
}