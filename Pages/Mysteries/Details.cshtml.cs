using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite; // Required for in-memory SQLite
using QueryMyst.Data;        // Replace with your actual Data namespace
using QueryMyst.Models;      // Replace with your actual Models namespace
using System.Threading.Tasks;
using System.Data.Common;    // Required for DbDataReader
using System.Text;           // Required for StringBuilder
using System.Text.Json;      // Required for JSON comparison
using System.Collections.Generic; // For List and Dictionary
using System.Linq;          // For LINQ methods like Select
using Microsoft.Extensions.Logging; // For ILogger
using System;                // For DateTime, Exception

namespace QueryMyst.Pages.Mysteries // Replace with your actual Pages namespace
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
        public string QueryResult { get; set; } // Formatted string for display
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

            // Optional: Load previous attempt if exists
            // var userMystery = await _context.UserMysteries.FirstOrDefaultAsync(um => um.UserId == user.Id && um.MysteryId == id);
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
                ModelState.AddModelError(nameof(UserQuery), ErrorMessage); // Add model error
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
                        // Ensure the solution query exists before trying to execute it
                        if (!string.IsNullOrWhiteSpace(Mystery.Details.SolutionQuery))
                        {
                           (solutionResultJson, _) = await ExecuteQueryAndGetJsonAsync(connection, Mystery.Details.SolutionQuery);

                           // 5. Compare Results
                           IsCorrectSolution = userResultJson != null && solutionResultJson != null && userResultJson == solutionResultJson;
                        }
                        else
                        {
                            _logger.LogWarning("Solution query is missing for Mystery ID {MysteryId}", id);
                            ErrorMessage = "Could not verify solution: Missing expected result query.";
                            IsCorrectSolution = false; // Cannot be correct if we can't compare
                        }

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
                    ErrorMessage = $"An unexpected error occurred during query execution: {ex.Message}";
                }
                finally
                {
                     await connection.CloseAsync();
                }
            }
            // --- End Query Execution ---

            // --- Update UserMystery Progress ---
            // Only proceed if there wasn't a setup/execution error before comparison
            if (string.IsNullOrEmpty(ErrorMessage) || executionError == null) // Allow saving progress even if query had logical errors but executed
            {
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
                        // userMystery.UserSolution = null;
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating UserMystery for User {UserId} and Mystery {MysteryId}", user.Id, id);
                    // Non-critical error, maybe add a message but don't block the user
                    // TempData["ProgressError"] = "Could not save progress.";
                }
            }
            // --- End UserMystery Update ---

            return Page();
        }

        // Helper to execute non-query SQL (Schema, Inserts)
        private async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return;
            // Basic split for simple scripts; might need more robust parsing for complex cases
            // Handle potential GO statements or other batch separators if necessary (not typical for SQLite setup)
            var commands = sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var commandText in commands)
            {
                var trimmedCommand = commandText.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedCommand))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = trimmedCommand;
                        try
                        {
                             await command.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex)
                        {
                             _logger.LogError(ex, "Error executing non-query command: {CommandText}", trimmedCommand);
                             throw; // Re-throw to be caught by the main try-catch
                        }
                    }
                }
            }
        }

        // Helper to execute a query and return results as JSON string and any error message
        private async Task<(string resultJson, string error)> ExecuteQueryAndGetJsonAsync(SqliteConnection connection, string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return (null, "Query cannot be empty.");
            }

            var results = new List<Dictionary<string, object>>();
            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.HasRows)
                        {
                            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); // Case-insensitive keys for consistency
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    // Handle potential DBNull values
                                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                    // Convert byte arrays (like blobs) to a readable string representation if needed
                                    if (value is byte[] bytes)
                                    {
                                        value = "BLOB_DATA"; // Or Convert.ToBase64String(bytes);
                                    }
                                    row[columns[i]] = value;
                                }
                                results.Add(row);
                            }
                        }
                    }
                }
                // Serialize consistently for comparison
                var options = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = null }; // Ensure consistent casing
                return (JsonSerializer.Serialize(results, options), null);
            }
            catch (SqliteException sqliteEx) // Catch specific SQLite errors
            {
                 _logger.LogWarning(sqliteEx, "SQLite Execution failed for query: {Query}", sql);
                 return (null, sqliteEx.Message); // Provide the specific SQLite error message
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "General Execution failed for query: {Query}", sql);
                return (null, ex.Message); // Provide a general error message
            }
        }


        // Helper to format JSON result data into a simple string table for display
        private string FormatResultFromJson(string json)
        {
             // Handle null or empty JSON input
             if (string.IsNullOrEmpty(json)) return "Query executed, but the result representation is empty.";

            try
            {
                var results = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

                // Handle cases where JSON deserializes but represents no data or an empty list
                if (results == null) return "Query executed, failed to interpret results.";
                if (results.Count == 0) return "Query executed successfully, but returned no rows.";


                var sb = new StringBuilder();
                // Get columns from the first row, assuming all rows have the same structure
                var columns = results[0].Keys.ToList();

                // Calculate maximum width for each column for better alignment (optional but nice)
                var columnWidths = columns.ToDictionary(col => col, col => col.Length);
                foreach (var row in results)
                {
                    foreach (var col in columns)
                    {
                        var cellValue = row.TryGetValue(col, out var value) ? (value?.ToString() ?? "NULL") : "NULL";
                        columnWidths[col] = Math.Max(columnWidths[col], cellValue.Length);
                    }
                }

                // Header
                sb.AppendLine(string.Join(" | ", columns.Select(col => col.PadRight(columnWidths[col]))));
                // Separator line based on calculated widths + padding/separators
                sb.AppendLine(new string('-', columns.Sum(col => columnWidths[col]) + (columns.Count - 1) * 3));

                // Rows
                foreach (var row in results)
                {
                    var values = columns.Select(col =>
                    {
                        var cellValue = row.TryGetValue(col, out var value) ? (value?.ToString() ?? "NULL") : "NULL";
                        return cellValue.PadRight(columnWidths[col]); // Pad for alignment
                    });
                    sb.AppendLine(string.Join(" | ", values));
                }
                return sb.ToString();
            }
             catch (JsonException jsonEx)
             {
                  _logger.LogError(jsonEx, "Error deserializing result JSON");
                  return "Could not format results due to invalid JSON structure.\nRaw JSON:\n" + json;
             }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting result JSON");
                // Return raw JSON in case of unexpected formatting errors
                return "Could not format results. Raw JSON:\n" + json;
            }
        }
    }
}