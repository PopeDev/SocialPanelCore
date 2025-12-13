using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SocialPanelCore.Domain.Entities;

namespace SocialPanelCore.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<SocialChannelConfig> SocialChannelConfigs => Set<SocialChannelConfig>();
    public DbSet<BasePost> BasePosts => Set<BasePost>();
    public DbSet<PostTargetNetwork> PostTargetNetworks => Set<PostTargetNetwork>();
    public DbSet<AdaptedPost> AdaptedPosts => Set<AdaptedPost>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<UserAccountAccess> UserAccountAccess => Set<UserAccountAccess>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configuración de Account
        builder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        // Configuración de User
        builder.Entity<User>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        });

        // Configuración de SocialChannelConfig
        builder.Entity<SocialChannelConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AccountId, e.NetworkType }).IsUnique();
            entity.Property(e => e.AccessToken).IsRequired();
            entity.Property(e => e.Handle).HasMaxLength(200);
            entity.HasOne(e => e.Account)
                .WithMany(a => a.SocialChannels)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de BasePost
        builder.Entity<BasePost>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.ApprovalNotes).HasMaxLength(1000);
            entity.Property(e => e.RejectionNotes).HasMaxLength(1000);
            entity.HasOne(e => e.Account)
                .WithMany(a => a.Posts)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedByUser)
                .WithMany(u => u.CreatedPosts)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de PostTargetNetwork
        builder.Entity<PostTargetNetwork>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.BasePost)
                .WithMany(p => p.TargetNetworks)
                .HasForeignKey(e => e.BasePostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de AdaptedPost
        builder.Entity<AdaptedPost>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.BasePostId, e.NetworkType }).IsUnique();
            entity.Property(e => e.AdaptedContent).IsRequired();
            entity.Property(e => e.ExternalPostId).HasMaxLength(500);
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.HasOne(e => e.BasePost)
                .WithMany(p => p.AdaptedVersions)
                .HasForeignKey(e => e.BasePostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de UserAccountAccess
        builder.Entity<UserAccountAccess>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.AccountId }).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany(u => u.AccountAccess)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Account)
                .WithMany(a => a.UserAccess)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ========== CONFIGURACIÓN DE PostMedia ==========
        builder.Entity<PostMedia>(entity =>
        {
            entity.HasKey(pm => pm.Id);

            entity.Property(pm => pm.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(pm => pm.StoredFileName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(pm => pm.RelativePath)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(pm => pm.ContentType)
                .IsRequired()
                .HasMaxLength(100);

            // Relación con BasePost
            entity.HasOne(pm => pm.BasePost)
                .WithMany(bp => bp.Media)
                .HasForeignKey(pm => pm.BasePostId)
                .OnDelete(DeleteBehavior.Cascade);

            // Índice para búsqueda por post
            entity.HasIndex(pm => pm.BasePostId);
        });

        // ========== CONFIGURACIÓN DE OAuthState ==========
        builder.Entity<OAuthState>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.State)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.RedirectUri)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.ReturnUrl)
                .HasMaxLength(500);

            entity.Property(e => e.CodeVerifier)
                .HasMaxLength(200);

            entity.Property(e => e.RequestedScopes)
                .HasMaxLength(500);

            // Índice único en State para búsquedas rápidas
            entity.HasIndex(e => e.State).IsUnique();

            // Índice para limpieza de estados expirados
            entity.HasIndex(e => e.ExpiresAt);
        });
    }
}
