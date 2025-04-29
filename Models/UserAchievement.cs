using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace QueryMyst.Models
{
    public class UserAchievement
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; }
        
        [Required]
        public int AchievementId { get; set; }
        
        [Required]
        public DateTime EarnedOn { get; set; }
        
        public bool NotificationDisplayed { get; set; }
        
        // Navigation properties
        public IdentityUser User { get; set; }
        public Achievement Achievement { get; set; }
    }
}