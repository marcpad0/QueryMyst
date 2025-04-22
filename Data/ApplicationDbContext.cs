using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QueryMyst.Models;
using Microsoft.AspNetCore.Identity; // Required for IdentityUser

namespace QueryMyst.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser> // Ensure IdentityUser is specified if not already
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        
        public DbSet<Mystery> Mysteries { get; set; }
        public DbSet<MysteryDetails> MysteryDetails { get; set; }
        public DbSet<UserMystery> UserMysteries { get; set; }
        
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            
            // Configure relationships
            builder.Entity<Mystery>()
                .HasOne(m => m.Details)
                .WithOne(d => d.Mystery)
                .HasForeignKey<MysteryDetails>(d => d.MysteryId);
                
            builder.Entity<UserMystery>()
                .HasOne(um => um.User)
                .WithMany() // IdentityUser doesn't have a direct collection navigation property back to UserMystery by default
                .HasForeignKey(um => um.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<UserMystery>()
                .HasOne(um => um.Mystery)
                .WithMany(m => m.UserMysteries)
                .HasForeignKey(um => um.MysteryId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // --- Configure Creator Relationship ---
            builder.Entity<Mystery>()
                .HasOne(m => m.Creator) // Navigation property in Mystery
                .WithMany() // IdentityUser doesn't have a direct collection navigation property back to created Mysteries by default
                .HasForeignKey(m => m.CreatorId) // Foreign key in Mystery
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting a user if they created mysteries. Choose Cascade if you want mysteries deleted when the creator is deleted.
            // --- End Configure Creator Relationship ---
                
            // Convert RequiredSkills list to JSON string
            builder.Entity<Mystery>()
                .Property(m => m.RequiredSkills)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions)null), // Use default options or create new ones
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions)null)); // Use default options or create new ones
        }
    }
}