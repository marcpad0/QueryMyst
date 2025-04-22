// filepath: e:\Visual Studio\Progetto\QueryMyst\Models\Mystery.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.AspNetCore.Identity; // Add this using

namespace QueryMyst.Models
{
    public class Mystery
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Title { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        [MaxLength(20)]
        public string Difficulty { get; set; }

        [MaxLength(50)]
        public string DifficultyClass { get; set; }

        [MaxLength(50)]
        public string Category { get; set; }

        [MaxLength(100)]
        public string Icon { get; set; }

        // --- Link to Creator ---
        [Required]
        public string CreatorId { get; set; } // Foreign key to IdentityUser

        [ForeignKey("CreatorId")]
        public IdentityUser Creator { get; set; } // Navigation property
        // --- End Link to Creator ---

        // Navigation property for mystery details
        public MysteryDetails Details { get; set; }

        // Navigation property for user solutions
        public ICollection<UserMystery> UserMysteries { get; set; }

        // Skills stored as JSON or in a separate table
        [NotMapped] // Keep this if you are storing skills as JSON in the same table
        public List<string> RequiredSkills { get; set; }
        // If you decide to make RequiredSkills a separate table later, remove [NotMapped]

        // Computed property to count users who have solved this mystery
        public int SolvedCount => UserMysteries?.Count(um => um.IsCompleted) ?? 0;
    }
}