using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Infrastructure.Identity;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

/// <summary>Gives the ASP.NET Core Identity tables plain names (snake_case is applied afterwards by convention).</summary>
internal sealed class IdentityTablesConfiguration :
    IEntityTypeConfiguration<ApplicationUser>,
    IEntityTypeConfiguration<ApplicationRole>,
    IEntityTypeConfiguration<IdentityUserRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>,
    IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");
        builder.Property(user => user.DisplayName).HasMaxLength(ApplicationUser.DisplayNameMaxLength).IsRequired();
        builder.Property(user => user.PreferredLanguage).HasMaxLength(10).IsRequired();
    }

    public void Configure(EntityTypeBuilder<ApplicationRole> builder) => builder.ToTable("Roles");

    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder) => builder.ToTable("UserRoles");

    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("UserClaims");

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("UserLogins");

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("UserTokens");

    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder) => builder.ToTable("RoleClaims");
}
