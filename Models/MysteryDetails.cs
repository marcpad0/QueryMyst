using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QueryMyst.Models
{
    public class MysteryDetails
    {
        public int Id { get; set; }
        
        [Required]
        public int MysteryId { get; set; }
        
        [Column(TypeName = "text")]
        public string FullDescription { get; set; }
        
        [Column(TypeName = "text")]
        public string FalseClues { get; set; }
        
        [Column(TypeName = "text")]
        public string SolutionQuery { get; set; }
        
        [Column(TypeName = "text")]
        public string HintText { get; set; }
        
        // Database schema for this mystery
        [Column(TypeName = "text")]
        public string DatabaseSchema { get; set; }
        
        [Column(TypeName = "text")]
        public string SampleData { get; set; }
        
        // Navigation property back to Mystery
        public Mystery Mystery { get; set; }
    }
}