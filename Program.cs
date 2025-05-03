using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QueryMyst.Data;
using QueryMyst.Models; // Add this using statement
using QueryMyst.Services; // Add this for the AchievementService
using System;          // Add this using statement
using System.Linq;     // Add this using statement
using System.Collections.Generic; // Add this using statement

var builder = WebApplication.CreateBuilder(args);

// Get connection string from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
}

// Add database context with SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Add Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // Add this line
builder.Services.AddHttpClient(); 

// Register Achievement Service
builder.Services.AddScoped<AchievementService>();

var app = builder.Build();

// Seed the database here
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated(); // Ensure the database exists
        SeedDatabase(context); // Call the seeding method
        SeedAchievements(context); // Call new method to seed achievements
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers(); // Add this line

app.Run();

// Add this static method to Program.cs (outside the main execution flow)
static void SeedDatabase(ApplicationDbContext context)
{
    // Check if mysteries already exist
    if (context.Mysteries.Any())
    {
        return; // DB has been seeded
    }

    // First, ensure we have a default user to use as the creator
    string systemUserId;
    var systemUser = context.Users.FirstOrDefault();

    if (systemUser == null)
    {
        // Create a default system user if none exists
        systemUser = new IdentityUser
        {
            UserName = "system@querymyst.com",
            NormalizedUserName = "SYSTEM@QUERYMYST.COM",
            Email = "system@querymyst.com",
            NormalizedEmail = "SYSTEM@QUERYMYST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        // Set a password for the system user
        var passwordHasher = new PasswordHasher<IdentityUser>();
        systemUser.PasswordHash = passwordHasher.HashPassword(systemUser, "SystemP@ss123!");

        context.Users.Add(systemUser);
        context.SaveChanges();
    }

    systemUserId = systemUser.Id;

    var mysteries = new List<Mystery>
    {
        new Mystery
        {
            Title = "The Missing Employee",
            Description = "An employee record exists, but they haven't logged any access time. Find their name.",
            Difficulty = "Beginner",
            DifficultyClass = "difficulty-beginner",
            Category = "Business Analytics",
            Icon = "<i class='bi bi-person-slash fs-1 text-info'></i>",
            RequiredSkills = new List<string> { "SELECT", "WHERE", "Subquery/JOIN" },
            CreatorId = systemUserId, // Set the creator ID here
            Details = new MysteryDetails
            {
                FullDescription = "The HR department has a list of all current employees in the 'Employees' table. The security team tracks building access in the 'AccessLogs' table. One employee seems to be on the payroll but has never accessed the building according to the logs. Identify this employee.",
                DatabaseSchema = @"
CREATE TABLE Employees (
    EmployeeID INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Department TEXT
);
CREATE TABLE AccessLogs (
    LogID INTEGER PRIMARY KEY,
    EmployeeID INTEGER,
    AccessTime DATETIME,
    FOREIGN KEY (EmployeeID) REFERENCES Employees(EmployeeID)
);",
                SampleData = @"
INSERT INTO Employees (EmployeeID, Name, Department) VALUES
(1, 'Alice Smith', 'Sales'),
(2, 'Bob Johnson', 'IT'),
(3, 'Charlie Brown', 'HR'),
(4, 'Diana Prince', 'Marketing');

INSERT INTO AccessLogs (LogID, EmployeeID, AccessTime) VALUES
(101, 1, '2025-04-20 08:00:00'),
(102, 2, '2025-04-20 08:05:00'),
(103, 1, '2025-04-20 17:00:00'),
(104, 4, '2025-04-21 09:00:00');
-- Note: Employee 3 (Charlie Brown) has no access logs.",
                SolutionQuery = "SELECT Name FROM Employees WHERE EmployeeID NOT IN (SELECT DISTINCT EmployeeID FROM AccessLogs WHERE EmployeeID IS NOT NULL); -- Alternative: SELECT e.Name FROM Employees e LEFT JOIN AccessLogs al ON e.EmployeeID = al.EmployeeID WHERE al.LogID IS NULL;",
                HintText = "You need to find an employee present in one table but absent from another. Think about using subqueries with NOT IN or using a LEFT JOIN and checking for NULL values.",
                FalseClues = "Maybe the employee works remotely? Check the 'Department' column. Is there a missing LogID sequence?"
            }
        },
        new Mystery
        {
            Title = "Highest Priced Gadget",
            Description = "Find the name and price of the most expensive gadget in the inventory.",
            Difficulty = "Beginner",
            DifficultyClass = "difficulty-beginner",
            Category = "Business Analytics",
            Icon = "<i class='bi bi-currency-dollar fs-1 text-success'></i>",
            RequiredSkills = new List<string> { "SELECT", "ORDER BY", "LIMIT" },
            CreatorId = systemUserId, // Set the creator ID here
            Details = new MysteryDetails
            {
                FullDescription = "The 'Products' table contains information about various items in stock, including their name and price. Your task is to identify the single most expensive product.",
                DatabaseSchema = @"
CREATE TABLE Products (
    ProductID INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Category TEXT,
    Price REAL
);",
                SampleData = @"
INSERT INTO Products (ProductID, Name, Category, Price) VALUES
(1, 'Laptop Pro', 'Electronics', 1200.50),
(2, 'Wireless Mouse', 'Accessories', 25.99),
(3, 'Mechanical Keyboard', 'Accessories', 75.00),
(4, 'Ultra HD Monitor', 'Electronics', 350.75),
(5, 'Gaming Chair', 'Furniture', 299.99);",
                SolutionQuery = "SELECT Name, Price FROM Products ORDER BY Price DESC LIMIT 1;",
                HintText = "How can you sort the products based on their price? Once sorted, how do you select only the top one?",
                FalseClues = "Perhaps the most expensive item is in a specific 'Category'? Does the 'ProductID' give a clue about the price?"
            }
        },
        // --- Intermediate Mystery ---
        new Mystery
        {
            Title = "Departmental Spending",
            Description = "Calculate the total spending for each department based on employee expenses.",
            Difficulty = "Intermediate",
            DifficultyClass = "difficulty-intermediate",
            Category = "Data Recovery", // Different Category
            Icon = "<i class='bi bi-calculator fs-1 text-warning'></i>",
            RequiredSkills = new List<string> { "SELECT", "JOIN", "GROUP BY", "SUM" },
            CreatorId = systemUserId,
            Details = new MysteryDetails
            {
                FullDescription = "The company tracks employee expenses in the 'Expenses' table, linked to the 'Employees' table which contains department information. Calculate the total amount spent by employees in each department.",
                DatabaseSchema = @"
CREATE TABLE Employees (
    EmployeeID INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Department TEXT NOT NULL
);
CREATE TABLE Expenses (
    ExpenseID INTEGER PRIMARY KEY,
    EmployeeID INTEGER,
    Amount REAL,
    ExpenseDate DATE,
    FOREIGN KEY (EmployeeID) REFERENCES Employees(EmployeeID)
);",
                SampleData = @"
INSERT INTO Employees (EmployeeID, Name, Department) VALUES
(1, 'Alice Smith', 'Sales'),
(2, 'Bob Johnson', 'IT'),
(3, 'Charlie Brown', 'Sales'),
(4, 'David Lee', 'IT');

INSERT INTO Expenses (ExpenseID, EmployeeID, Amount, ExpenseDate) VALUES
(1, 1, 50.00, '2025-03-10'),
(2, 2, 120.50, '2025-03-11'),
(3, 1, 75.25, '2025-03-15'),
(4, 3, 30.00, '2025-03-18'),
(5, 2, 85.00, '2025-03-20');",
                SolutionQuery = "SELECT e.Department, SUM(ex.Amount) AS TotalSpending FROM Employees e JOIN Expenses ex ON e.EmployeeID = ex.EmployeeID GROUP BY e.Department ORDER BY TotalSpending DESC;",
                HintText = "You'll need to combine information from both tables. How can you group the results by department and calculate the sum of expenses for each group?",
                FalseClues = "Are there employees with no expenses? Does the date matter for this calculation?"
            }
        },
        // --- Advanced Mystery ---
        new Mystery
        {
            Title = "Consecutive Login Days",
            Description = "Find users who logged in for 3 or more consecutive days.",
            Difficulty = "Advanced",
            DifficultyClass = "difficulty-advanced",
            Category = "Security Audit", // Different Category
            Icon = "<i class='bi bi-calendar-check fs-1 text-danger'></i>",
            RequiredSkills = new List<string> { "SELECT", "Window Functions", "LAG/LEAD", "Date Functions", "Subquery/CTE" },
            CreatorId = systemUserId,
            Details = new MysteryDetails
            {
                FullDescription = "The 'UserLogins' table records each time a user logs into the system. Identify the usernames of users who have logged in on at least three consecutive days.",
                DatabaseSchema = @"
CREATE TABLE Users (
    UserID INTEGER PRIMARY KEY,
    Username TEXT NOT NULL UNIQUE
);
CREATE TABLE UserLogins (
    LoginID INTEGER PRIMARY KEY,
    UserID INTEGER,
    LoginDate DATE,
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);",
                SampleData = @"
INSERT INTO Users (UserID, Username) VALUES (1, 'jdoe'), (2, 'asmith'), (3, 'bwilliams');
INSERT INTO UserLogins (LoginID, UserID, LoginDate) VALUES
(1, 1, '2025-04-01'), (2, 1, '2025-04-02'), (3, 1, '2025-04-03'), -- jdoe 3 consecutive
(4, 2, '2025-04-01'), (5, 2, '2025-04-03'), (6, 2, '2025-04-04'), -- asmith not consecutive
(7, 3, '2025-04-05'), (8, 3, '2025-04-06'), (9, 1, '2025-04-06'), -- bwilliams 2 consecutive, jdoe breaks streak
(10, 3, '2025-04-07'), (11, 3, '2025-04-08'); -- bwilliams 4 consecutive",
                SolutionQuery = @"
WITH LoginGaps AS (
    SELECT
        UserID,
        LoginDate,
        DATE(LoginDate, '-' || ROW_NUMBER() OVER (PARTITION BY UserID ORDER BY LoginDate) || ' days') AS GroupDate
    FROM (SELECT DISTINCT UserID, LoginDate FROM UserLogins) -- Ensure unique login dates per user
),
ConsecutiveGroups AS (
    SELECT UserID, GroupDate, COUNT(*) AS ConsecutiveDays
    FROM LoginGaps
    GROUP BY UserID, GroupDate
)
SELECT DISTINCT u.Username
FROM ConsecutiveGroups cg
JOIN Users u ON cg.UserID = u.UserID
WHERE cg.ConsecutiveDays >= 3;",
                HintText = "Think about how to identify sequences. Window functions like ROW_NUMBER() can help create groups. Subtracting a row number (within a user's ordered logins) from the date can create a constant value for consecutive days.",
                FalseClues = "Simply counting logins per user won't work. Using LAG might be complex to track streaks longer than 2 days directly."
            }
        },
        // --- Expert Mystery ---
        new Mystery
        {
            Title = "Organizational Hierarchy Path",
            Description = "For a given employee, find their full management chain up to the CEO.",
            Difficulty = "Expert",
            DifficultyClass = "difficulty-expert",
            Category = "Algorithm Challenge", // Different Category
            Icon = "<i class='bi bi-diagram-3 fs-1 text-primary'></i>",
            RequiredSkills = new List<string> { "Recursive CTE", "SELECT", "JOIN", "String Aggregation" },
            CreatorId = systemUserId,
            Details = new MysteryDetails
            {
                FullDescription = "The 'Employees' table stores employee information, including their manager (ManagerID). The CEO has a NULL ManagerID. For employee 'Charlie', find the full path of their management hierarchy, starting from Charlie up to the CEO, displayed as 'Charlie -> Bob -> Alice -> CEO'.",
                DatabaseSchema = @"
CREATE TABLE Employees (
    EmployeeID INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    ManagerID INTEGER, -- NULL for CEO
    FOREIGN KEY (ManagerID) REFERENCES Employees(EmployeeID)
);",
                SampleData = @"
INSERT INTO Employees (EmployeeID, Name, ManagerID) VALUES
(1, 'Alice (CEO)', NULL),
(2, 'Bob', 1),
(3, 'Charlie', 2),
(4, 'David', 1),
(5, 'Eve', 2);",
                SolutionQuery = @"
WITH RECURSIVE ManagementChain AS (
    -- Anchor member: Start with the employee 'Charlie'
    SELECT EmployeeID, Name, ManagerID, Name AS Path
    FROM Employees
    WHERE Name = 'Charlie'

    UNION ALL

    -- Recursive member: Join with the manager
    SELECT e.EmployeeID, e.Name, e.ManagerID, mc.Path || ' -> ' || e.Name
    FROM Employees e
    JOIN ManagementChain mc ON e.EmployeeID = mc.ManagerID
    WHERE e.ManagerID IS NOT NULL -- Stop before the CEO is added again via Path
)
-- Select the longest path which includes the CEO implicitly at the end
SELECT Path || ' -> ' || (SELECT Name FROM Employees WHERE ManagerID IS NULL) AS HierarchyPath
FROM ManagementChain
ORDER BY LENGTH(Path) DESC
LIMIT 1;",
                HintText = "This requires traversing a hierarchy. Recursive Common Table Expressions (CTEs) are ideal for this. Start with the target employee and recursively join to find their manager until you reach the top (NULL ManagerID).",
                FalseClues = "Simple joins won't work for an unknown hierarchy depth. Window functions aren't designed for this type of recursive traversal."
            }
        }
    };

    context.Mysteries.AddRange(mysteries);
    context.SaveChanges();
}

// Add a new method to seed achievements
static void SeedAchievements(ApplicationDbContext context)
{
    if (context.Achievements.Any())
    {
        return; // Achievements already seeded
    }

    var achievements = new List<Achievement>
    {
        // --- Solving Mysteries ---
        new Achievement { Name = "First Steps", Description = "Solve your first mystery.", Icon = "<i class='bi bi-signpost-split'></i>", Category = "Beginner", Criteria = "FirstMystery", PointsValue = 10 },
        new Achievement { Name = "Apprentice Detective", Description = "Solve 5 mysteries.", Icon = "<i class='bi bi-search'></i>", Category = "Progress", Criteria = "Solve5Mysteries", PointsValue = 25 },
        new Achievement { Name = "Seasoned Investigator", Description = "Solve 10 mysteries.", Icon = "<i class='bi bi-binoculars'></i>", Category = "Progress", Criteria = "Solve10Mysteries", PointsValue = 50 },
        new Achievement { Name = "Master Sleuth", Description = "Solve 25 mysteries.", Icon = "<i class='bi bi-fingerprint'></i>", Category = "Progress", Criteria = "Solve25Mysteries", PointsValue = 100 },
        new Achievement { Name = "Legendary Detective", Description = "Solve 50 mysteries.", Icon = "<i class='bi bi-trophy'></i>", Category = "Mastery", Criteria = "Solve50Mysteries", PointsValue = 200 },
        new Achievement { Name = "Beginner Graduate", Description = "Solve 3 Beginner mysteries.", Icon = "<i class='bi bi-mortarboard'></i>", Category = "Skill", Criteria = "Solve3Beginner", PointsValue = 15 },
        new Achievement { Name = "Intermediate Challenger", Description = "Solve 3 Intermediate mysteries.", Icon = "<i class='bi bi-bar-chart-steps'></i>", Category = "Skill", Criteria = "Solve3Intermediate", PointsValue = 30 },
        new Achievement { Name = "Advanced Tactician", Description = "Solve 3 Advanced mysteries.", Icon = "<i class='bi bi-graph-up-arrow'></i>", Category = "Skill", Criteria = "Solve3Advanced", PointsValue = 60 },
        new Achievement { Name = "Expert Virtuoso", Description = "Solve 1 Expert mystery.", Icon = "<i class='bi bi-gem'></i>", Category = "Mastery", Criteria = "Solve1Expert", PointsValue = 100 },
        new Achievement { Name = "Grandmaster", Description = "Solve 3 Expert mysteries.", Icon = "<i class='bi bi-award'></i>", Category = "Mastery", Criteria = "Solve3Expert", PointsValue = 250 },

        // --- Writing Queries (Attempts) ---
        new Achievement { Name = "Query Starter", Description = "Write 10 queries.", Icon = "<i class='bi bi-keyboard'></i>", Category = "Activity", Criteria = "Write10Queries", PointsValue = 5 },
        new Achievement { Name = "Query Enthusiast", Description = "Write 50 queries.", Icon = "<i class='bi bi-terminal'></i>", Category = "Activity", Criteria = "Write50Queries", PointsValue = 20 },
        new Achievement { Name = "Query Pro", Description = "Write 100 queries.", Icon = "<i class='bi bi-braces'></i>", Category = "Activity", Criteria = "Write100Queries", PointsValue = 40 },
        new Achievement { Name = "Query Master", Description = "Write 250 queries.", Icon = "<i class='bi bi-database-gear'></i>", Category = "Mastery", Criteria = "Write250Queries", PointsValue = 80 },
        new Achievement { Name = "Query Legend", Description = "Write 500 queries.", Icon = "<i class='bi bi-cpu'></i>", Category = "Mastery", Criteria = "Write500Queries", PointsValue = 150 },
        new Achievement { Name = "The Thousandth Query", Description = "Write 1000 queries.", Icon = "<i class='bi bi-infinity'></i>", Category = "Mastery", Criteria = "Write1000Queries", PointsValue = 300 },

        // --- Creating Mysteries ---
        new Achievement { Name = "Mystery Architect", Description = "Create your first mystery.", Icon = "<i class='bi bi-pencil-square'></i>", Category = "Creator", Criteria = "CreateFirstMystery", PointsValue = 50 },
        new Achievement { Name = "Prolific Creator", Description = "Create 5 mysteries.", Icon = "<i class='bi bi-journal-plus'></i>", Category = "Creator", Criteria = "Create5Mysteries", PointsValue = 150 },
        new Achievement { Name = "Master Storyteller", Description = "Create 10 mysteries.", Icon = "<i class='bi bi-book'></i>", Category = "Creator", Criteria = "Create10Mysteries", PointsValue = 300 },

        // --- Streaks / Consistency (Example - Requires more logic) ---
        // new Achievement { Name = "Daily Detective", Description = "Solve a mystery 3 days in a row.", Icon = "<i class='bi bi-calendar-check'></i>", Category = "Activity", Criteria = "Solve3DaysRow", PointsValue = 30 },

        // --- Specific Skills (Example - Requires more logic/tags) ---
        // new Achievement { Name = "Join Master", Description = "Solve 5 mysteries requiring JOINs.", Icon = "<i class='bi bi-diagram-3'></i>", Category = "Skill", Criteria = "Solve5Joins", PointsValue = 40 },
        // new Achievement { Name = "Subquery Specialist", Description = "Solve 3 mysteries requiring subqueries.", Icon = "<i class='bi bi-box-arrow-in-down-right'></i>", Category = "Skill", Criteria = "Solve3Subqueries", PointsValue = 50 },

        // Add more achievements up to 50...
        new Achievement { Name = "Data Digger", Description = "Solve 5 mysteries in the 'Data Recovery' category.", Icon = "<i class='bi bi-tools'></i>", Category = "Skill", Criteria = "Solve5DataRecovery", PointsValue = 40 },
        new Achievement { Name = "Business Whiz", Description = "Solve 5 mysteries in the 'Business Analytics' category.", Icon = "<i class='bi bi-briefcase'></i>", Category = "Skill", Criteria = "Solve5Business", PointsValue = 40 },
        new Achievement { Name = "Security Sentinel", Description = "Solve 5 mysteries in the 'Security Audit' category.", Icon = "<i class='bi bi-shield-check'></i>", Category = "Skill", Criteria = "Solve5Security", PointsValue = 40 },
        new Achievement { Name = "Puzzle Solver", Description = "Solve 5 mysteries in the 'General Puzzle' category.", Icon = "<i class='bi bi-puzzle'></i>", Category = "Skill", Criteria = "Solve5Puzzle", PointsValue = 40 },
        new Achievement { Name = "Algorithm Ace", Description = "Solve 5 mysteries in the 'Algorithm Challenge' category.", Icon = "<i class='bi bi-gear-wide-connected'></i>", Category = "Skill", Criteria = "Solve5Algorithm", PointsValue = 40 },

        new Achievement { Name = "Persistent Prober", Description = "Attempt 10 different mysteries.", Icon = "<i class='bi bi-door-open'></i>", Category = "Activity", Criteria = "Attempt10Mysteries", PointsValue = 15 },
        new Achievement { Name = "Curious Explorer", Description = "Attempt 25 different mysteries.", Icon = "<i class='bi bi-compass'></i>", Category = "Activity", Criteria = "Attempt25Mysteries", PointsValue = 35 },
        new Achievement { Name = "Dedicated Detective", Description = "Attempt 50 different mysteries.", Icon = "<i class='bi bi-journal-bookmark'></i>", Category = "Activity", Criteria = "Attempt50Mysteries", PointsValue = 70 },

        new Achievement { Name = "Quick Learner", Description = "Solve a Beginner mystery within 5 attempts.", Icon = "<i class='bi bi-lightbulb'></i>", Category = "Skill", Criteria = "SolveBeginnerUnder5", PointsValue = 10 },
        new Achievement { Name = "Efficient Analyst", Description = "Solve an Intermediate mystery within 10 attempts.", Icon = "<i class='bi bi-clipboard-data'></i>", Category = "Skill", Criteria = "SolveIntermediateUnder10", PointsValue = 20 },
        new Achievement { Name = "Sharp Mind", Description = "Solve an Advanced mystery within 15 attempts.", Icon = "<i class='bi bi-brain'></i>", Category = "Skill", Criteria = "SolveAdvancedUnder15", PointsValue = 40 },
        new Achievement { Name = "Precision Expert", Description = "Solve an Expert mystery within 20 attempts.", Icon = "<i class='bi bi-bullseye'></i>", Category = "Skill", Criteria = "SolveExpertUnder20", PointsValue = 80 },

        // ... Continue adding more diverse achievements ...

        new Achievement { Name = "Century Solver", Description = "Solve 100 mysteries.", Icon = "<i class='bi bi-100'></i>", Category = "Mastery", Criteria = "Solve100Mysteries", PointsValue = 400 },
        new Achievement { Name = "Double Century", Description = "Solve 200 mysteries.", Icon = "<i class='bi bi-200'></i>", Category = "Mastery", Criteria = "Solve200Mysteries", PointsValue = 800 },

        new Achievement { Name = "Creator Contributor", Description = "Have one of your created mysteries solved by 10 users.", Icon = "<i class='bi bi-people'></i>", Category = "Creator", Criteria = "MysterySolved10Times", PointsValue = 75 },
        new Achievement { Name = "Popular Author", Description = "Have one of your created mysteries solved by 50 users.", Icon = "<i class='bi bi-star'></i>", Category = "Creator", Criteria = "MysterySolved50Times", PointsValue = 200 },

        new Achievement { Name = "Perfect Score", Description = "Solve a mystery on the first attempt.", Icon = "<i class='bi bi-check2-all'></i>", Category = "Skill", Criteria = "SolveFirstAttempt", PointsValue = 25 },
        new Achievement { Name = "Five First Tries", Description = "Solve 5 different mysteries on the first attempt.", Icon = "<i class='bi bi-5-circle'></i>", Category = "Skill", Criteria = "Solve5FirstAttempt", PointsValue = 125 },

        // Add 10 more to reach 50
        new Achievement { Name = "Intermediate Expert", Description = "Solve 10 Intermediate mysteries.", Icon = "<i class='bi bi-bar-chart-line'></i>", Category = "Skill", Criteria = "Solve10Intermediate", PointsValue = 75 },
        new Achievement { Name = "Advanced Pro", Description = "Solve 10 Advanced mysteries.", Icon = "<i class='bi bi-graph-up'></i>", Category = "Skill", Criteria = "Solve10Advanced", PointsValue = 150 },
        new Achievement { Name = "Expert Elite", Description = "Solve 10 Expert mysteries.", Icon = "<i class='bi bi-diamond-half'></i>", Category = "Mastery", Criteria = "Solve10Expert", PointsValue = 500 },

        new Achievement { Name = "Query Artisan", Description = "Write 750 queries.", Icon = "<i class='bi bi-tools'></i>", Category = "Mastery", Criteria = "Write750Queries", PointsValue = 220 },
        new Achievement { Name = "Query Sage", Description = "Write 1500 queries.", Icon = "<i class='bi bi-mortarboard-fill'></i>", Category = "Mastery", Criteria = "Write1500Queries", PointsValue = 450 },

        new Achievement { Name = "Esteemed Creator", Description = "Create 15 mysteries.", Icon = "<i class='bi bi-award-fill'></i>", Category = "Creator", Criteria = "Create15Mysteries", PointsValue = 400 },
        new Achievement { Name = "Legendary Author", Description = "Create 25 mysteries.", Icon = "<i class='bi bi-trophy-fill'></i>", Category = "Creator", Criteria = "Create25Mysteries", PointsValue = 600 },

        new Achievement { Name = "Tenacious Tinkerer", Description = "Spend over 100 attempts on a single mystery (solved or not).", Icon = "<i class='bi bi-hammer'></i>", Category = "Activity", Criteria = "Over100AttemptsOneMystery", PointsValue = 50 },
        new Achievement { Name = "Category Connoisseur", Description = "Solve at least one mystery in every category.", Icon = "<i class='bi bi-tags'></i>", Category = "Progress", Criteria = "SolveEachCategory", PointsValue = 75 },
        new Achievement { Name = "Difficulty Dominator", Description = "Solve at least one mystery in every difficulty level.", Icon = "<i class='bi bi-distribute-vertical'></i>", Category = "Progress", Criteria = "SolveEachDifficulty", PointsValue = 75 },

    };

    context.Achievements.AddRange(achievements);
    context.SaveChanges();
}