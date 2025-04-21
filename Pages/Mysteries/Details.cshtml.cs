using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite; // Required for in-memory SQLite
using QueryMyst.Data;
using QueryMyst.Models;
using System.Threading.Tasks;
using System.Data.Common; // Required for DbDataReader
using System.Text; // Required for StringBuilder
using System.Text.Json; // Required for JSON comparison

namespace QueryMyst.Pages.Mysteries
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(ApplicationDbContext context, UserManager<IdentityUser> userManager, ILogger<DetailsModel> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public Mystery Mystery { get; set; }
        public string QueryResult { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsCorrectSolution { get; set; } = false;
        public bool IsPost { get; private set; } = false;

        [BindProperty]
        public string UserQuery { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            IsPost = false; // Explicitly set to false on GET
            Mystery = await _context.Mysteries
                                    .Include(m => m.Details)
                                    .FirstOrDefaultAsync(m => m.Id == id);

            if (Mystery == null || Mystery.Details == null)
            {
                _logger.LogWarning("Mystery with ID {MysteryId} not found or details missing.", id);
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            _logger.LogInformation("User {UserName} accessed details for Mystery ID {MysteryId}", user?.UserName ?? "Unknown", id);

            // Load previous attempt if exists? (Optional)
            // var userMystery = await _context.UserMysteries
            //     .FirstOrDefaultAsync(um => um.UserId == user.Id && um.MysteryId == id);
            // if (userMystery != null) { UserQuery = userMystery.UserSolution; }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            IsPost = true; // Set to true when handling a POST request
            Mystery = await _context.Mysteries
                                    .Include(m => m.Details)
                                    .FirstOrDefaultAsync(m => m.Id == id);

            if (Mystery == null || Mystery.Details == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge(); // Should not happen if [Authorize] is working
            }

            _logger.LogInformation("User {UserName} submitted query for Mystery ID {MysteryId}: {Query}", user.UserName, id, UserQuery);

            if (string.IsNullOrWhiteSpace(UserQuery))
            {
                ErrorMessage = "Please enter a SQL query.";
                return Page();
            }

            // --- Query Execution Logic ---
            string userResultJson = null;
            string solutionResultJson = null;
            string executionError = null;

            // Use a unique in-memory database for each execution
            using (var connection = new SqliteConnection("DataSource=:memory:"))
            {
                await connection.OpenAsync();

                try
                {
                    // 1. Create Schema
                    await ExecuteNonQueryAsync(connection, Mystery.Details.DatabaseSchema);

                    // 2. Insert Sample Data
                    await ExecuteNonQueryAsync(connection, Mystery.Details.SampleData);

                    // 3. Execute User Query
                    (userResultJson, executionError) = await ExecuteQueryAndGetJsonAsync(connection, UserQuery);

                    if (executionError == null)
                    {
                        // 4. Execute Solution Query (only if user query succeeded)
                        (solutionResultJson, _) = await ExecuteQueryAndGetJsonAsync(connection, Mystery.Details.SolutionQuery);

                        // 5. Compare Results
                        IsCorrectSolution = userResultJson != null && userResultJson == solutionResultJson;
                        QueryResult = FormatResultFromJson(userResultJson); // Display formatted user result
                    }
                    else
                    {
                        ErrorMessage = $"Error executing your query: {executionError}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting up/executing query for Mystery {MysteryId}", id);
                    ErrorMessage = $"An unexpected error occurred: {ex.Message}";
                }
                finally
                {
                     await connection.CloseAsync();
                }
            }
            // --- End Query Execution ---

            // --- Update UserMystery Progress ---
            try
            {
                var userMystery = await _context.UserMysteries
                    .FirstOrDefaultAsync(um => um.UserId == user.Id && um.MysteryId == id);

                if (userMystery == null)
                {
                    userMystery = new UserMystery
                    {
                        UserId = user.Id,
                        MysteryId = id,
                        StartedAt = DateTime.UtcNow,
                        AttemptsCount = 0,
                        IsCompleted = false
                    };
                    _context.UserMysteries.Add(userMystery);
                }

                userMystery.AttemptsCount++;
                userMystery.UserSolution = UserQuery; // Store last attempt

                if (IsCorrectSolution && !userMystery.IsCompleted)
                {
                    userMystery.IsCompleted = true;
                    userMystery.CompletedAt = DateTime.UtcNow;
                    // Optionally clear UserSolution on success if desired
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error updating UserMystery for User {UserId} and Mystery {MysteryId}", user.Id, id);
                 // Non-critical error, maybe add a message but don't block the user
                 // ErrorMessage += "\n(Could not save progress)";
            }
            // --- End UserMystery Update ---

            return Page();
        }

        // Helper to execute non-query SQL (Schema, Inserts)
        private async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return;
            // Basic split for simple scripts; might need more robust parsing for complex cases
            var commands = sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var commandText in commands)
            {
                if (!string.IsNullOrWhiteSpace(commandText))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = commandText.Trim();
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        // Helper to execute a query and return results as JSON string
        private async Task<(string resultJson, string error)> ExecuteQueryAndGetJsonAsync(SqliteConnection connection, string sql)
        {
            var results = new List<Dictionary<string, object>>();
            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                // Handle potential DBNull values
                                row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                            results.Add(row);
                        }
                    }
                }
                // Serialize consistently for comparison
                var options = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = null }; // Ensure consistent casing
                return (JsonSerializer.Serialize(results, options), null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SQL Execution failed for query: {Query}", sql);
                return (null, ex.Message);
            }
        }

        // Helper to format JSON result data into a simple string table (optional)
        private string FormatResultFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "No results returned.";

            try
            {
                var results = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
                if (results == null || results.Count == 0) return "Query executed successfully, but returned no rows.";

                var sb = new StringBuilder();
                var columns = results[0].Keys.ToList();

                // Header
                sb.AppendLine(string.Join(" | ", columns));
                sb.AppendLine(new string('-', sb.Length - 2)); // Separator line

                // Rows
                foreach (var row in results)
                {
                    var values = columns.Select(col => row.TryGetValue(col, out var value) ? (value?.ToString() ?? "NULL") : "NULL");
                    sb.AppendLine(string.Join(" | ", values));
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting result JSON");
                return "Could not format results. Raw JSON:\n" + json;
            }
        }
    }
}