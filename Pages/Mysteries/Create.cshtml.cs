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
using Microsoft.AspNetCore.Mvc.Filters; // Needed for AntiForgeryToken validation
using System.Data.Common; // Needed for DbDataReader
using System.Text; // Needed for StringBuilder

namespace QueryMyst.Pages.Mysteries
{
    [Authorize]
    [ValidateAntiForgeryToken] // Add this attribute to automatically validate antiforgery tokens on POST handlers
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

            // --- Start SQL Syntax Validation (Keep as final check) ---
            bool sqlValid = await ValidateSqlAsync();
            if (!sqlValid)
            {
                _logger.LogWarning("SQL validation failed during final submission by user: {User}", User.Identity?.Name);
                return Page();
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

        // --- Handler for Schema Validation ---
        public async Task<JsonResult> OnPostValidateSchemaAsync([FromBody] SqlValidationRequest data)
        {
            if (string.IsNullOrWhiteSpace(data?.SchemaSql))
            {
                return new JsonResult(new { success = false, message = "Schema SQL cannot be empty." });
            }

            using (var connection = new SqliteConnection("DataSource=:memory:"))
            {
                try
                {
                    await connection.OpenAsync();
                    await ExecuteNonQuerySqlAsync(connection, data.SchemaSql);
                    
                    // Validate column types with sqlite_master
                    var validationErrors = await ValidateSchemaTypesAsync(connection);
                    if (validationErrors.Count > 0)
                    {
                        return new JsonResult(new { 
                            success = false, 
                            message = $"Schema contains invalid data types: {string.Join(", ", validationErrors)}" 
                        });
                    }
                    
                    await connection.CloseAsync();
                    return new JsonResult(new { success = true, message = "Schema syntax appears valid." });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AJAX Schema validation failed.");
                    await connection.CloseAsync(); // Ensure closed on error
                    return new JsonResult(new { success = false, message = $"Schema Error: {ex.Message}" });
                }
            }
        }

        private async Task<List<string>> ValidateSchemaTypesAsync(SqliteConnection connection)
        {
            var errors = new List<string>();
            
            // Expanded list of valid SQLite types and common aliases
            var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                // Core SQLite types
                "INTEGER", "TEXT", "BLOB", "REAL", "NUMERIC", 
                "VARCHAR", "CHAR", "NVARCHAR", "NCHAR", "CHARACTER", 
                "INT", "BIGINT", "SMALLINT", "TINYINT", 
                "FLOAT", "DOUBLE", "DECIMAL",
                "BOOLEAN", "BOOL",
                "DATETIME", "DATE", "TIME", "TIMESTAMP"
            };
            
            using (var command = connection.CreateCommand())
            {
                // Query the schema information from sqlite_master
                command.CommandText = "SELECT name, sql FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite\\_%' ESCAPE '\\'";
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var tableName = reader.GetString(0);
                        var createSql = reader.GetString(1);
        
                        // Now check column definitions
                        using (var columnsCommand = connection.CreateCommand())
                        {
                            columnsCommand.CommandText = $"PRAGMA table_info({tableName})";
                            using (var columnsReader = await columnsCommand.ExecuteReaderAsync())
                            {
                                while (await columnsReader.ReadAsync())
                                {
                                    var columnName = columnsReader.GetString(1);
                                    var typeName = columnsReader.GetString(2);
                                    
                                    // Extract base type (remove size or constraints)
                                    var baseType = typeName.Split('(')[0].Trim().ToUpperInvariant();
                                    
                                    if (!validTypes.Contains(baseType))
                                    {
                                        errors.Add($"Column '{columnName}' in table '{tableName}' has invalid type '{typeName}' (should be one of: {string.Join(", ", validTypes)})");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            return errors;
        }

        // --- Handler for Sample Data Validation ---
        public async Task<JsonResult> OnPostValidateDataAsync([FromBody] SqlValidationRequest data)
        {
            if (string.IsNullOrWhiteSpace(data?.SchemaSql) || string.IsNullOrWhiteSpace(data?.DataSql))
            {
                return new JsonResult(new { success = false, message = "Schema and Sample Data SQL cannot be empty." });
            }

            using (var connection = new SqliteConnection("DataSource=:memory:"))
            {
                try
                {
                    await connection.OpenAsync();
                    // Execute schema first
                    await ExecuteNonQuerySqlAsync(connection, data.SchemaSql);
                    
                    // Execute data
                    await ExecuteNonQuerySqlAsync(connection, data.DataSql);
                    
                    // Validate data types match schema expectations
                    var typeErrors = await ValidateDataTypesAsync(connection);
                    if (typeErrors.Count > 0)
                    {
                        return new JsonResult(new { 
                            success = false, 
                            message = $"Data type validation failed: {string.Join("; ", typeErrors)}" 
                        });
                    }
                    
                    await connection.CloseAsync();
                    return new JsonResult(new { success = true, message = "Sample Data syntax appears valid and compatible with schema." });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AJAX Data validation failed.");
                    await connection.CloseAsync(); // Ensure closed on error
                    return new JsonResult(new { success = false, message = $"Data Error: {ex.Message}" });
                }
            }
        }

        private async Task<List<string>> ValidateDataTypesAsync(SqliteConnection connection)
        {
            var errors = new List<string>();
            
            // Get all tables
            using (var tablesCommand = connection.CreateCommand())
            {
                tablesCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                using (var tablesReader = await tablesCommand.ExecuteReaderAsync())
                {
                    while (await tablesReader.ReadAsync())
                    {
                        var tableName = tablesReader.GetString(0);
                        var columnTypes = new Dictionary<string, string>();
                        
                        // Get column info for each table
                        using (var columnsCommand = connection.CreateCommand())
                        {
                            columnsCommand.CommandText = $"PRAGMA table_info({tableName})";
                            using (var columnsReader = await columnsCommand.ExecuteReaderAsync())
                            {
                                while (await columnsReader.ReadAsync())
                                {
                                    var columnName = columnsReader.GetString(1);
                                    var typeName = columnsReader.GetString(2).ToUpperInvariant();
                                    columnTypes[columnName] = typeName;
                                }
                            }
                        }
                        
                        // Check each numeric column for non-numeric values
                        foreach (var column in columnTypes)
                        {
                            if (column.Value.Contains("INT") || column.Value.Contains("REAL") || 
                                column.Value.Contains("NUMERIC") || column.Value.Contains("DECIMAL") ||
                                column.Value.Contains("FLOAT"))
                            {
                                using (var checkCommand = connection.CreateCommand())
                                {
                                    // Try to perform a numeric operation - this will fail if data is not numeric
                                    checkCommand.CommandText = $"SELECT COUNT(*) FROM {tableName} " + 
                                        $"WHERE typeof({column.Key}) != 'integer' AND typeof({column.Key}) != 'real' " + 
                                        $"AND {column.Key} IS NOT NULL";
                                    
                                    var nonNumericCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                                    if (nonNumericCount > 0)
                                    {
                                        errors.Add($"Table '{tableName}' column '{column.Key}' contains non-numeric values but has type '{column.Value}'");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            return errors;
        }

        // --- Handler for Solution Query Validation & Preview ---
        public async Task<JsonResult> OnPostValidateQueryAsync([FromBody] SqlValidationRequest data)
        {
            if (string.IsNullOrWhiteSpace(data?.SchemaSql) || string.IsNullOrWhiteSpace(data?.DataSql) || string.IsNullOrWhiteSpace(data?.QuerySql))
            {
                return new JsonResult(new { success = false, message = "Schema, Sample Data, and Solution Query SQL cannot be empty." });
            }

            using (var connection = new SqliteConnection("DataSource=:memory:"))
            {
                try
                {
                    await connection.OpenAsync();
                    // Execute schema and data
                    await ExecuteNonQuerySqlAsync(connection, data.SchemaSql);
                    await ExecuteNonQuerySqlAsync(connection, data.DataSql);

                    // Execute the query and get results
                    var (resultJson, error) = await ExecuteQueryAndGetJsonAsync(connection, data.QuerySql); // Use the JSON helper

                    await connection.CloseAsync();

                    if (error != null)
                    {
                        return new JsonResult(new { success = false, message = $"Query Error: {error}" });
                    }
                    else
                    {
                        // Format result for preview (optional, can be done client-side too)
                        string formattedResult = FormatResultFromJson(resultJson);
                        return new JsonResult(new { success = true, message = "Query executed successfully.", results = formattedResult }); // Send formatted result
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AJAX Query validation failed.");
                    await connection.CloseAsync(); // Ensure closed on error
                    return new JsonResult(new { success = false, message = $"Query Execution Error: {ex.Message}" });
                }
            }
        }

        // --- Model for AJAX Requests ---
        public class SqlValidationRequest
        {
            public string SchemaSql { get; set; }
            public string DataSql { get; set; }
            public string QuerySql { get; set; }
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
            using (var connection = new SqliteConnection("DataSource=:memory:"))
            {
                try
                {
                    await connection.OpenAsync();

                    // 1. Validate Schema
                    try
                    {
                        await ExecuteNonQuerySqlAsync(connection, Input.DatabaseSchema);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "SQL Schema validation failed.");
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
                    if (isValid)
                    {
                        try
                        {
                            var (_, error) = await ExecuteQueryAndCheckErrorAsync(connection, Input.SolutionQuery);
                            if (error != null)
                            {
                                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.SolutionQuery)}", $"Solution Query Error: {error}");
                                isValid = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "SQL Solution Query validation failed unexpectedly.");
                            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.SolutionQuery)}", $"Solution Query Error: {ex.Message}");
                            isValid = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to open in-memory database for SQL validation.");
                    ModelState.AddModelError(string.Empty, "Could not initialize SQL validation environment.");
                    isValid = false;
                }
                finally
                {
                    if (connection.State == System.Data.ConnectionState.Open)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            return isValid;
        }

        private async Task ExecuteNonQuerySqlAsync(SqliteConnection connection, string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return;

            var commands = sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var commandText in commands)
            {
                var trimmedCommand = commandText.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedCommand))
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = trimmedCommand;
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }

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
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) { }
                    }
                }
                return ("{}", null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SQL Execution failed during validation for query: {Query}", sql);
                return (null, ex.Message);
            }
        }

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
                        if (!reader.HasRows) return ("[]", null);

                        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                        while (await reader.ReadAsync())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            }
                            results.Add(row);
                        }
                    }
                }
                var options = new JsonSerializerOptions { WriteIndented = false };
                return (JsonSerializer.Serialize(results, options), null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SQL Execution failed for query: {Query}", sql);
                return (null, ex.Message);
            }
        }

        private string FormatResultFromJson(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "[]") return "<p><i>Query executed successfully, but returned no results.</i></p>";

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var results = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json, options);

                if (results == null || results.Count == 0) return "<p><i>Query executed successfully, but returned no results.</i></p>";

                var sb = new StringBuilder();
                sb.Append("<table class='table table-sm table-bordered table-striped small'>");
                sb.Append("<thead><tr>");

                foreach (var key in results[0].Keys)
                {
                    sb.Append($"<th>{System.Web.HttpUtility.HtmlEncode(key)}</th>");
                }
                sb.Append("</tr></thead>");
                sb.Append("<tbody>");

                foreach (var row in results)
                {
                    sb.Append("<tr>");
                    foreach (var value in row.Values)
                    {
                        sb.Append($"<td>{System.Web.HttpUtility.HtmlEncode(value?.ToString() ?? "NULL")}</td>");
                    }
                    sb.Append("</tr>");
                }

                sb.Append("</tbody></table>");
                return sb.ToString();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error formatting JSON result: {Json}", json);
                return "<p class='text-danger'>Error displaying results.</p>";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error formatting result JSON: {Json}", json);
                return "<p class='text-danger'>Unexpected error displaying results.</p>";
            }
        }
    }
}