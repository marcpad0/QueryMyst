using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QueryMyst.Data;
using QueryMyst.Models; // Add this using statement
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

app.Run();

// Add this static method to Program.cs (outside the main execution flow)
static void SeedDatabase(ApplicationDbContext context)
{
    // Check if mysteries already exist
    if (context.Mysteries.Any())
    {
        return; // DB has been seeded
    }

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
        }
    };

    context.Mysteries.AddRange(mysteries);
    context.SaveChanges();
}