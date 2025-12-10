using Microsoft.EntityFrameworkCore;
using SocialPanelCore.Domain.Entities;

namespace SocialPanelCore.Infrastructure.Data;

public class SocialPanelDbContext : DbContext
{
    public SocialPanelDbContext(DbContextOptions<SocialPanelDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BasePost> BasePosts => Set<BasePost>();
    public DbSet<TargetNetwork> TargetNetworks => Set<TargetNetwork>();
    public DbSet<SocialChannelConfig> SocialChannelConfigs => Set<SocialChannelConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(256);
        });

        modelBuilder.Entity<BasePost>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(5000);
            entity.HasOne(e => e.Account)
                .WithMany(a => a.Posts)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TargetNetwork>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.BasePost)
                .WithMany(p => p.TargetNetworks)
                .HasForeignKey(e => e.BasePostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SocialChannelConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Handle).HasMaxLength(256);
            entity.HasOne(e => e.Account)
                .WithMany(a => a.SocialChannels)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
