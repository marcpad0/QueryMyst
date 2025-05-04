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
        public string SolutionQuery { get; set; }
        
        [Column(TypeName = "text")]
        public string HintText { get; set; }
        
        // Database schema for this mystery
        [Column(TypeName = "text")]
        public string DatabaseSchema { get; set; }
        
        [Column(TypeName = "text")]
        public string SampleData { get; set; }

        // New property for expected output columns
        [Display(Name = "Expected Output Columns")]
        [Column(TypeName = "text")]
        public string ExpectedOutputColumns { get; set; }
        
        // Navigation property back to Mystery
        public Mystery Mystery { get; set; }
    }
}