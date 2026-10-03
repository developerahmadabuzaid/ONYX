using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Infrastructure.Identity;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class SessionTablesConfiguration :
    IEntityTypeConfiguration<RolePermission>,
    IEntityTypeConfiguration<UserSession>,
    IEntityTypeConfiguration<RefreshToken>,
    IEntityTypeConfiguration<LoginChallenge>,
    IEntityTypeConfiguration<DataProtectionKeyRecord>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(grant => new { grant.RoleId, grant.Permission });
        builder.Property(grant => grant.Permission).HasMaxLength(100);
        builder.HasOne<ApplicationRole>().WithMany().HasForeignKey(grant => grant.RoleId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.RevocationReason).HasMaxLength(UserSession.RevocationReasonMaxLength);
        builder.Property(session => session.IpAddress).HasMaxLength(UserSession.IpAddressMaxLength);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(session => session.UserId);
    }

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasMaxLength(RefreshToken.HashLength).IsFixedLength().IsRequired();
        builder.HasOne(token => token.Session).WithMany(session => session.RefreshTokens).HasForeignKey(token => token.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(token => token.TokenHash).IsUnique();
    }

    public void Configure(EntityTypeBuilder<LoginChallenge> builder)
    {
        builder.ToTable("LoginChallenges");
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.Id).ValueGeneratedNever();
        builder.Property(challenge => challenge.Purpose).HasConversion<string>().HasMaxLength(30);
        builder.Property(challenge => challenge.TokenHash).HasMaxLength(RefreshToken.HashLength).IsFixedLength().IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(challenge => challenge.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(challenge => challenge.TokenHash).IsUnique();
    }

    public void Configure(EntityTypeBuilder<DataProtectionKeyRecord> builder)
    {
        builder.ToTable("DataProtectionKeys");
        builder.HasKey(key => key.Id);
        builder.Property(key => key.Id).ValueGeneratedNever();
        builder.Property(key => key.FriendlyName).HasMaxLength(200);
        builder.Property(key => key.Xml).IsRequired();
    }
}
