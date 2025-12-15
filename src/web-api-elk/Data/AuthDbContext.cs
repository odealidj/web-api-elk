using Microsoft.EntityFrameworkCore;
using web_api_elk.Models;

namespace web_api_elk.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var user = modelBuilder.Entity<User>();
        user.HasKey(u => u.Id);
        user.HasIndex(u => u.Username).IsUnique();
        user.Property(u => u.Username).IsRequired().HasMaxLength(100);
        user.Property(u => u.Email).HasMaxLength(200);
        user.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
        user.Property(u => u.CreatedAt).IsRequired();
    }
}
