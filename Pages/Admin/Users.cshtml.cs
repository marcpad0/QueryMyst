using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QueryMyst.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class UsersModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UsersModel> _logger;

        public UsersModel(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UsersModel> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [BindProperty]
        public string UserId { get; set; }

        [BindProperty]
        public string UserEmail { get; set; }

        [BindProperty]
        public List<string> SelectedRoles { get; set; }

        public List<IdentityUser> Users { get; set; }
        public Dictionary<string, IList<string>> UserRoles { get; set; }
        public List<IdentityRole> AllRoles { get; set; }
        
        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Users = await _userManager.Users.ToListAsync();
            UserRoles = new Dictionary<string, IList<string>>();
            
            foreach (var user in Users)
            {
                UserRoles[user.Id] = await _userManager.GetRolesAsync(user);
            }
            
            AllRoles = await _roleManager.Roles.ToListAsync();
            
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            if (string.IsNullOrEmpty(UserId))
            {
                StatusMessage = "Error: User ID not specified.";
                return RedirectToPage();
            }

            var user = await _userManager.FindByIdAsync(UserId);
            if (user == null)
            {
                StatusMessage = "Error: User not found.";
                return RedirectToPage();
            }

            // Don't allow admins to delete themselves
            if (user.Email == User.Identity.Name)
            {
                StatusMessage = "Error: You cannot delete your own account.";
                return RedirectToPage();
            }

            // Delete related data
            var result = await _userManager.DeleteAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("Admin deleted user {Email}", user.Email);
                StatusMessage = $"User {user.Email} has been deleted.";
            }
            else
            {
                StatusMessage = "Error: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditRolesAsync()
        {
            if (string.IsNullOrEmpty(UserId))
            {
                StatusMessage = "Error: User ID not specified.";
                return RedirectToPage();
            }

            var user = await _userManager.FindByIdAsync(UserId);
            if (user == null)
            {
                StatusMessage = "Error: User not found.";
                return RedirectToPage();
            }

            // Get current roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            
            // Remove user from all current roles
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            
            // Add user to selected roles
            if (SelectedRoles != null && SelectedRoles.Any())
            {
                await _userManager.AddToRolesAsync(user, SelectedRoles);
            }
            
            _logger.LogInformation("Admin updated roles for user {Email}", user.Email);
            StatusMessage = $"Roles updated for {user.Email}.";
            
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditEmailAsync()
        {
            if (string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(UserEmail))
            {
                StatusMessage = "Error: User ID or Email not specified.";
                return RedirectToPage();
            }

            var user = await _userManager.FindByIdAsync(UserId);
            if (user == null)
            {
                StatusMessage = "Error: User not found.";
                return RedirectToPage();
            }

            // Update email
            user.Email = UserEmail;
            user.NormalizedEmail = UserEmail.ToUpper();
            user.UserName = UserEmail;
            user.NormalizedUserName = UserEmail.ToUpper();
            
            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("Admin updated email for user {Email} to {NewEmail}", user.Email, UserEmail);
                StatusMessage = $"Email updated for user to {UserEmail}.";
            }
            else
            {
                StatusMessage = "Error: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }
            
            return RedirectToPage();
        }
    }
}