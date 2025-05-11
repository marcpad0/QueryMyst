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
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Configuration; // Added for IConfiguration
using System.Net.Http;                   // Added for HttpClient
using System.Net.Http.Headers;           // Added for MediaTypeWithQualityHeaderValue
using System.Net.Http.Json;
using System.Text.Json.Serialization;    // Added for ReadFromJsonAsync
using System.Text.Json.Nodes;            // Required for JsonObject

namespace QueryMyst.Pages.Mysteries
{
    [Authorize]
    [ValidateAntiForgeryToken]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CreateModel> _logger;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration; // Added
        private readonly IHttpClientFactory _httpClientFactory; // Added

        public CreateModel(
            ApplicationDbContext context,
            ILogger<CreateModel> logger,
            UserManager<IdentityUser> userManager,
            IConfiguration configuration, // Added
            IHttpClientFactory httpClientFactory) // Added
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _configuration = configuration; // Added
            _httpClientFactory = httpClientFactory; // Added
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

            [Display(Name = "Expected Output Columns (comma-separated)")]
            [MaxLength(500, ErrorMessage = "Expected columns cannot exceed 500 characters.")]
            [DataType(DataType.Text)]
            public string ExpectedOutputColumns { get; set; }

            [Display(Name = "Hint Text")]
            [DataType(DataType.MultilineText)]
            public string HintText { get; set; }
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
                ExpectedOutputColumns = Input.ExpectedOutputColumns,
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

        // --- NEW: Model for Gemini Request ---
        public class GeminiRequestData
        {
            public string Topic { get; set; }
        }

        // --- NEW: Model for Gemini Response (remains the same) ---
        public class GeneratedMysteryData
        {
            [JsonPropertyName("title")]
            public string Title { get; set; }
            [JsonPropertyName("description")]
            public string Description { get; set; }
            [JsonPropertyName("fullDescription")]
            public string FullDescription { get; set; }
            [JsonPropertyName("difficulty")]
            public string Difficulty { get; set; }
            [JsonPropertyName("category")]
            public string Category { get; set; }
            [JsonPropertyName("icon")]
            public string Icon { get; set; }
            [JsonPropertyName("requiredSkills")]
            public string RequiredSkills { get; set; }
            [JsonPropertyName("databaseSchema")]
            public string DatabaseSchema { get; set; }
            [JsonPropertyName("sampleData")]
            public string SampleData { get; set; }
            [JsonPropertyName("solutionQuery")]
            public string SolutionQuery { get; set; }
            [JsonPropertyName("hintText")]
            public string HintText { get; set; }
            [JsonPropertyName("expectedOutputColumns")]
            public string ExpectedOutputColumns { get; set; }
        }

        // --- NEW: Handler for Gemini Generation (Using Structured Output Schema) ---
        public async Task<JsonResult> OnPostGenerateMysteryAsync([FromBody] GeminiRequestData data)
        {
            if (string.IsNullOrWhiteSpace(data?.Topic))
            {
                return new JsonResult(new { success = false, message = "Topic cannot be empty." }) { StatusCode = 400 };
            }

            _logger.LogInformation("Attempting to generate mystery with AI for topic: {Topic} using structured output.", data.Topic);

            string apiKey = _configuration["GeminiApiKey"];
            string apiUrl = _configuration["GeminiApiUrl"];

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiUrl))
            {
                _logger.LogError("Gemini API Key or URL is not configured.");
                return new JsonResult(new { success = false, message = "AI service is not configured correctly." }) { StatusCode = 500 };
            }

            // --- Define the JSON Schema ---
            // This schema mirrors the GeneratedMysteryData class structure.
            // Using JsonObject for dynamic creation, matching the OpenAPI subset used by Gemini.
            var schema = new JsonObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JsonObject
                {
                    ["title"] = new JsonObject { ["type"] = "STRING", ["description"] = "Short, catchy title (max 100 chars)." },
                    ["description"] = new JsonObject { ["type"] = "STRING", ["description"] = "Brief summary (max 500 chars)." },
                    ["fullDescription"] = new JsonObject { ["type"] = "STRING", ["description"] = "Detailed scenario/story." },
                    ["difficulty"] = new JsonObject {
                        ["type"] = "STRING",
                        ["enum"] = new JsonArray("Beginner", "Intermediate", "Advanced", "Expert"),
                        ["description"] = "Difficulty level."
                    },
                    ["category"] = new JsonObject {
                        ["type"] = "STRING",
                        ["enum"] = new JsonArray("Business Analytics", "Data Recovery", "Security Audit", "General Puzzle", "Algorithm Challenge"),
                        ["description"] = "Mystery category."
                    },
                    ["icon"] = new JsonObject { ["type"] = "STRING", ["description"] = "Simple Bootstrap 5 icon HTML (e.g., <i class='bi bi-search'></i>)." },
                    ["requiredSkills"] = new JsonObject { ["type"] = "STRING", ["description"] = "Comma-separated list of relevant SQL skills." },
                    ["databaseSchema"] = new JsonObject { ["type"] = "STRING", ["description"] = "Valid SQLite 'CREATE TABLE' statements." },
                    ["sampleData"] = new JsonObject { ["type"] = "STRING", ["description"] = "Valid SQLite 'INSERT INTO' statements." },
                    ["solutionQuery"] = new JsonObject { ["type"] = "STRING", ["description"] = "A single, valid SQLite 'SELECT' statement." },
                    ["expectedOutputColumns"] = new JsonObject { ["type"] = "STRING", ["description"] = "Comma-separated list of the exact column names expected in the solution query's output, in order." },
                    ["hintText"] = new JsonObject { ["type"] = "STRING", ["description"] = "Obligatory hint." },
                    
                },
                // Define required fields if necessary (optional)
                ["required"] = new JsonArray(
                    "title", "fullDescription", "difficulty", "category",
                    "databaseSchema", "sampleData", "solutionQuery"
                    // Add others like "description", "icon", "requiredSkills" if they should always be present
                )
                // Optional: Define property ordering if specific order is desired
                // ["propertyOrdering"] = new JsonArray("title", "description", "difficulty", "category", ...)
            };


            // --- Simplified Prompt ---
            // Focus on the task, less on the format details as the schema handles that.
            string prompt = $@"Generate a complete SQL mystery for a platform called QueryMyst, based on the topic: '{data.Topic}'.

            Follow these specific requirements:

            1. DATABASE STRUCTURE:
            - Create tables based on the difficulty level and category and if user required it
            - Include foreign key constraints between related tables
            - Use appropriate primary keys and indexes
            - Incorporate a variety of data types (TEXT, INTEGER, REAL, TIMESTAMP, etc.)
            - Design a normalized database schema following best practices

            2. QUERY COMPLEXITY:
            - The solution should require multiple JOIN operations if tables are present
            - Avoid solution queries that not use JOINs if a lot of tables are present
            - Use appropriate WHERE clauses for filtering
            - Incorporate ORDER BY for meaningful result sorting
            - Consider using subqueries, CTEs, or window functions for Advanced/Expert levels

            3. DIFFICULTY MATCHING:
            - Beginner: Simple joins between 1-3 tables, basic filtering
            - Intermediate: Multi-table joins, aggregations, grouping
            - Advanced: Complex joins, subqueries, multiple conditions
            - Expert: Advanced SQL features, optimization challenges, complex problem-solving

            4. NARRATIVE QUALITY:
            - Create an engaging storyline that provides context for the database
            - Make the mystery scenario realistic and professionally relevant
            - Ensure the scenario matches the selected category (Business Analytics, Security Audit, etc.)
            - Clearly define what the user needs to discover through SQL

            5. EDUCATIONAL VALUE:
            - Design the mystery to teach specific SQL concepts
            - Include hints that guide users toward the right approach
            - Ensure the expected output columns accurately reflect the solution
            
            6. JSON SCHEMA:
            - The output must be a valid JSON object matching the provided schema.
            - Include all required fields and their descriptions
            - Ensure the JSON is well-formed and valid according to the schema.
            - Always provide a icon HTML string for the mystery
            

            Populate all fields according to the provided JSON schema, ensuring all SQL is valid for SQLite.";


            // --- Prepare API Request with Schema ---
            var requestPayload = new JsonObject
            {
                ["contents"] = new JsonArray(
                    new JsonObject
                    {
                        ["parts"] = new JsonArray(
                            new JsonObject { ["text"] = prompt }
                        )
                    }
                ),
                ["generationConfig"] = new JsonObject
                {
                    ["response_mime_type"] = "application/json",
                    ["response_schema"] = schema
                    // Optional: Add temperature, maxOutputTokens etc. here if needed
                    // ["temperature"] = 0.7,
                    // ["maxOutputTokens"] = 4096
                }
            };

            string responseBody = null;
            string mysteryJson = null; // Still useful for logging on error

            try
            {
                HttpClient client = _httpClientFactory.CreateClient();
                string requestUri = $"{apiUrl}?key={apiKey}";

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(120);

                // Serialize the JsonObject payload
                request.Content = new StringContent(requestPayload.ToJsonString(), Encoding.UTF8, "application/json");

                _logger.LogInformation("Sending request to Gemini API at {ApiUrl} with structured output schema.", apiUrl);
                HttpResponseMessage response = await client.SendAsync(request);

                responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API request failed with status code {StatusCode}. Response: {ResponseBody}", response.StatusCode, responseBody);
                    string errorMessage = $"AI service request failed (Status: {response.StatusCode}).";
                    try
                    {
                        var errorDetails = JsonDocument.Parse(responseBody);
                        if (errorDetails.RootElement.TryGetProperty("error", out var errorProp) && errorProp.TryGetProperty("message", out var msgProp))
                        {
                            errorMessage += $" Details: {msgProp.GetString()}";
                        }
                    }
                    catch { /* Ignore parsing error */ }
                    return new JsonResult(new { success = false, message = errorMessage }) { StatusCode = (int)response.StatusCode };
                }

                _logger.LogInformation("Received successful response from Gemini API.");

                GeneratedMysteryData generatedMystery = null;
                try
                {
                    // With structured output, the response *should* directly contain the JSON object
                    // matching the schema, potentially still wrapped in the standard API structure.
                    using (JsonDocument document = JsonDocument.Parse(responseBody))
                    {
                        if (document.RootElement.TryGetProperty("candidates", out var candidates) &&
                            candidates.ValueKind == JsonValueKind.Array &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out var content) &&
                            content.TryGetProperty("parts", out var parts) &&
                            parts.ValueKind == JsonValueKind.Array &&
                            parts.GetArrayLength() > 0 &&
                            // The 'text' part should now contain the JSON object defined by the schema
                            parts[0].TryGetProperty("text", out var textPart))
                        {
                            mysteryJson = textPart.GetString(); // Get the JSON string
                            if (string.IsNullOrWhiteSpace(mysteryJson))
                            {
                                 throw new JsonException("Received empty text part containing the JSON object from Gemini API.");
                            }

                            _logger.LogDebug("Attempting to deserialize Gemini JSON (structured output): {MysteryJson}", mysteryJson);
                            // Deserialize the JSON string extracted from the 'text' part
                            generatedMystery = JsonSerializer.Deserialize<GeneratedMysteryData>(mysteryJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        }
                    }

                    if (generatedMystery == null)
                    {
                        _logger.LogWarning("Could not find or parse the expected JSON content within the 'text' part in Gemini API response. Response Body: {ResponseBody}", responseBody);
                        throw new JsonException("Could not find or parse the expected mystery JSON content in the Gemini API response structure.");
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Failed to deserialize JSON response from Gemini (structured output). Attempted JSON: {MysteryJson}. Raw Response Body: {ResponseBody}", mysteryJson ?? "[mysteryJson not extracted]", responseBody);
                    return new JsonResult(new { success = false, message = $"Failed to parse structured response from AI service: {jsonEx.Message}" }) { StatusCode = 500 };
                }

                _logger.LogInformation("Successfully generated and parsed mystery using structured output for topic: {Topic}", data.Topic);
                return new JsonResult(new { success = true, mystery = generatedMystery });

            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request error calling Gemini API for topic: {Topic}", data.Topic);
                return new JsonResult(new { success = false, message = $"Network error contacting AI service: {httpEx.Message}" }) { StatusCode = 503 };
            }
            catch (TaskCanceledException timeoutEx)
            {
                 _logger.LogError(timeoutEx, "Gemini API request timed out for topic: {Topic}", data.Topic);
                 return new JsonResult(new { success = false, message = "AI service request timed out. Please try again." }) { StatusCode = 504 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error generating mystery with AI for topic: {Topic}. Raw Response Body: {ResponseBody}", data.Topic, responseBody ?? "[Response body not available]");
                return new JsonResult(new { success = false, message = $"An unexpected error occurred during AI generation: {ex.Message}" }) { StatusCode = 500 };
            }
        }

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

        private async Task<bool> ValidateSqlAsync()
        {
            bool isValid = true;
            using (var connection = new SqliteConnection("DataSource=:memory:"))
            {
                try
                {
                    await connection.OpenAsync();

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