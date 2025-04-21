using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QueryMyst.Models;

namespace QueryMyst.Data
{
    public class ApplicationDbContext : IdentityDbContext
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
                .WithMany()
                .HasForeignKey(um => um.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            builder.Entity<UserMystery>()
                .HasOne(um => um.Mystery)
                .WithMany(m => m.UserMysteries)
                .HasForeignKey(um => um.MysteryId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Convert RequiredSkills list to JSON string
            builder.Entity<Mystery>()
                .Property(m => m.RequiredSkills)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, new System.Text.Json.JsonSerializerOptions()),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, new System.Text.Json.JsonSerializerOptions()));
        }
    }
}