using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QueryMyst.Models
{
    public class Achievement
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Name { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Description { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Icon { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Category { get; set; }
        
        [Required]
        public string Criteria { get; set; }
        
        public int PointsValue { get; set; } = 10;
        
        // Navigation property
        public ICollection<UserAchievement> UserAchievements { get; set; }
    }
}