using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace QueryMyst.Models
{
    public class UserMystery
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; }
        
        [Required]
        public int MysteryId { get; set; }
        
        public bool IsCompleted { get; set; }
        
        public DateTime? StartedAt { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        [Column(TypeName = "text")]
        public string UserSolution { get; set; }
        
        public int AttemptsCount { get; set; }
        
        // Navigation properties
        public IdentityUser User { get; set; }
        public Mystery Mystery { get; set; }
    }
}