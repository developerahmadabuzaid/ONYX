using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Billing;
using Onyx.Domain.Customers;
using Onyx.Domain.Products;
using Onyx.Domain.Subscriptions;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

/// <summary>
/// Customer accounts (isolated from the staff <c>users</c> table: no foreign key between them, no shared Identity tables),
/// their tokens and sessions. Customers are erased in place, never deleted, so subscriptions and invoices keep their owner.
/// </summary>
internal sealed class CustomerAccountConfiguration : IEntityTypeConfiguration<CustomerAccount>
{
    public void Configure(EntityTypeBuilder<CustomerAccount> builder)
    {
        builder.ToTable("CustomerAccounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.Property(account => account.Email).HasMaxLength(CustomerAccount.EmailMaxLength).IsRequired();
        builder.Property(account => account.NormalizedEmail).HasMaxLength(CustomerAccount.EmailMaxLength).IsRequired();
        builder.Property(account => account.DisplayName).HasMaxLength(CustomerAccount.DisplayNameMaxLength).IsRequired();
        builder.Property(account => account.Company).HasMaxLength(CustomerAccount.CompanyMaxLength);
        builder.Property(account => account.PreferredLanguage).HasMaxLength(5).IsRequired();
        builder.Property(account => account.PasswordHash).HasMaxLength(CustomerAccount.PasswordHashMaxLength).IsRequired();
        builder.Property(account => account.SecurityStamp).HasMaxLength(CustomerAccount.SecurityStampLength).IsRequired();
        builder.Property(account => account.TermsVersion).HasMaxLength(CustomerAccount.TermsVersionMaxLength).IsRequired();
        builder.Property(account => account.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(account => account.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(account => account.CanSignIn);
        builder.HasIndex(account => account.NormalizedEmail).IsUnique();
        builder.HasIndex(account => new { account.Status, account.CreatedAtUtc });
    }
}

internal sealed class CustomerTokenConfiguration : IEntityTypeConfiguration<CustomerToken>
{
    public void Configure(EntityTypeBuilder<CustomerToken> builder)
    {
        builder.ToTable("CustomerTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasMaxLength(CustomerToken.HashLength).IsFixedLength().IsRequired();
        builder.Property(token => token.Purpose).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasOne<CustomerAccount>().WithMany().HasForeignKey(token => token.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.CustomerId, token.Purpose });
    }
}

internal sealed class CustomerSessionConfiguration : IEntityTypeConfiguration<CustomerSession>
{
    public void Configure(EntityTypeBuilder<CustomerSession> builder)
    {
        builder.ToTable("CustomerSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.TokenHash).HasMaxLength(CustomerSession.HashLength).IsFixedLength().IsRequired();
        builder.Property(session => session.SecurityStamp).HasMaxLength(CustomerAccount.SecurityStampLength).IsRequired();
        builder.Property(session => session.RevocationReason).HasMaxLength(CustomerSession.ReasonMaxLength);
        builder.Property(session => session.IpAddress).HasMaxLength(CustomerSession.IpAddressMaxLength);
        builder.HasOne<CustomerAccount>().WithMany().HasForeignKey(session => session.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(session => session.TokenHash).IsUnique();
        builder.HasIndex(session => session.CustomerId);
    }
}

internal sealed class SaasProductConfiguration : IEntityTypeConfiguration<SaasProduct>
{
    public void Configure(EntityTypeBuilder<SaasProduct> builder)
    {
        builder.ToTable("SaasProducts");
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).ValueGeneratedNever();
        builder.Property(product => product.Key).HasMaxLength(SaasProduct.KeyMaxLength).IsRequired();
        builder.Property(product => product.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(product => product.DemoKind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(product => product.DemoUrl).HasMaxLength(SaasProduct.DemoUrlMaxLength);
        builder.Property(product => product.VerificationSource).HasMaxLength(SaasProduct.VerificationSourceMaxLength);
        builder.Property(product => product.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(product => product.IsVerified);
        builder.Ignore(product => product.OffersTrial);
        builder.HasMany(product => product.Translations).WithOne().HasForeignKey(translation => translation.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(product => product.Plans).WithOne().HasForeignKey(plan => plan.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(product => product.Translations).HasField("_translations").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(product => product.Plans).HasField("_plans").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(product => product.Key).IsUnique();
        builder.HasIndex(product => new { product.Status, product.SortOrder });
    }
}

internal sealed class ProductTranslationConfiguration : IEntityTypeConfiguration<ProductTranslation>
{
    public void Configure(EntityTypeBuilder<ProductTranslation> builder)
    {
        builder.ToTable("SaasProductTranslations");
        builder.HasKey(translation => translation.Id);
        builder.Property(translation => translation.Id).ValueGeneratedNever();
        builder.Property(translation => translation.Language).HasMaxLength(5).IsRequired();
        builder.Property(translation => translation.Name).HasMaxLength(ProductTranslation.NameMaxLength).IsRequired();
        builder.Property(translation => translation.Slug).HasMaxLength(Domain.Content.Slug.MaxLength).IsRequired();
        builder.Property(translation => translation.Summary).HasMaxLength(ProductTranslation.SummaryMaxLength).IsRequired();
        builder.Property(translation => translation.Description).HasMaxLength(ProductTranslation.DescriptionMaxLength).IsRequired();
        builder.HasIndex(translation => new { translation.ProductId, translation.Language }).IsUnique();
        builder.HasIndex(translation => new { translation.Language, translation.Slug }).IsUnique();
    }
}

internal sealed class ProductPlanConfiguration : IEntityTypeConfiguration<ProductPlan>
{
    public void Configure(EntityTypeBuilder<ProductPlan> builder)
    {
        builder.ToTable("SaasProductPlans");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();
        builder.Property(plan => plan.Code).HasMaxLength(ProductPlan.CodeMaxLength).IsRequired();
        builder.Property(plan => plan.Price).HasPrecision(12, 2);
        builder.Property(plan => plan.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(plan => plan.BillingPeriod).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Ignore(plan => plan.ShowsPrice);
        builder.HasMany(plan => plan.Translations).WithOne().HasForeignKey(translation => translation.PlanId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(plan => plan.Translations).HasField("_translations").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(plan => new { plan.ProductId, plan.Code }).IsUnique();
    }
}

internal sealed class ProductPlanTranslationConfiguration : IEntityTypeConfiguration<ProductPlanTranslation>
{
    public void Configure(EntityTypeBuilder<ProductPlanTranslation> builder)
    {
        builder.ToTable("SaasProductPlanTranslations");
        builder.HasKey(translation => translation.Id);
        builder.Property(translation => translation.Id).ValueGeneratedNever();
        builder.Property(translation => translation.Language).HasMaxLength(5).IsRequired();
        builder.Property(translation => translation.Name).HasMaxLength(ProductPlan.NameMaxLength).IsRequired();
        builder.Property(translation => translation.Description).HasMaxLength(ProductPlan.DescriptionMaxLength);
        builder.HasIndex(translation => new { translation.PlanId, translation.Language }).IsUnique();
    }
}

internal sealed class CustomerSubscriptionConfiguration : IEntityTypeConfiguration<CustomerSubscription>
{
    public const string OneOpenPerProductIndex = "ux_customer_subscriptions_one_open_per_product";
    public const string OneTrialPerProductIndex = "ux_customer_subscriptions_one_trial_per_product";

    public void Configure(EntityTypeBuilder<CustomerSubscription> builder)
    {
        builder.ToTable("CustomerSubscriptions");
        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.Id).ValueGeneratedNever();
        builder.Property(subscription => subscription.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(subscription => subscription.CustomerNote).HasMaxLength(CustomerSubscription.NoteMaxLength);
        builder.Property(subscription => subscription.StaffNote).HasMaxLength(CustomerSubscription.NoteMaxLength);
        builder.Property(subscription => subscription.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(subscription => subscription.IsOpen);
        builder.HasOne<CustomerAccount>().WithMany().HasForeignKey(subscription => subscription.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SaasProduct>().WithMany().HasForeignKey(subscription => subscription.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductPlan>().WithMany().HasForeignKey(subscription => subscription.PlanId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(subscription => new { subscription.Status, subscription.RequestedAtUtc });

        // The service checks both rules first; the database guarantees them under concurrency.
        builder.HasIndex(subscription => new { subscription.CustomerId, subscription.ProductId })
            .IsUnique().HasFilter("status IN ('Requested', 'Trial', 'Active')").HasDatabaseName(OneOpenPerProductIndex);
        builder.HasIndex(subscription => new { subscription.CustomerId, subscription.ProductId, subscription.IsTrial })
            .IsUnique().HasFilter("is_trial").HasDatabaseName(OneTrialPerProductIndex);
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public const string NumberSequence = "invoice_number_seq";

    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(invoice => invoice.Id);
        builder.Property(invoice => invoice.Id).ValueGeneratedNever();
        builder.Property(invoice => invoice.Number).HasMaxLength(Invoice.NumberMaxLength);
        builder.Property(invoice => invoice.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(invoice => invoice.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(invoice => invoice.Total).HasPrecision(14, 2);
        builder.Property(invoice => invoice.Notes).HasMaxLength(Invoice.NotesMaxLength);
        builder.Property(invoice => invoice.BillToName).HasMaxLength(CustomerAccount.CompanyMaxLength);
        builder.Property(invoice => invoice.BillToEmail).HasMaxLength(CustomerAccount.EmailMaxLength);
        builder.Property(invoice => invoice.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(invoice => invoice.IsVisibleToCustomer);
        builder.HasMany(invoice => invoice.Lines).WithOne().HasForeignKey(line => line.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(invoice => invoice.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<CustomerAccount>().WithMany().HasForeignKey(invoice => invoice.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerSubscription>().WithMany().HasForeignKey(invoice => invoice.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(invoice => invoice.Number).IsUnique();
        builder.HasIndex(invoice => new { invoice.CustomerId, invoice.Status });
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.Description).HasMaxLength(InvoiceLine.DescriptionMaxLength).IsRequired();
        builder.Property(line => line.Quantity).HasPrecision(10, 2);
        builder.Property(line => line.UnitPrice).HasPrecision(12, 2);
        builder.Property(line => line.Amount).HasPrecision(14, 2);
        builder.HasIndex(line => new { line.InvoiceId, line.Position }).IsUnique();
    }
}

internal sealed class AiDemoRunConfiguration : IEntityTypeConfiguration<AiDemoRun>
{
    public void Configure(EntityTypeBuilder<AiDemoRun> builder)
    {
        builder.ToTable("AiDemoRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).ValueGeneratedNever();
        builder.Property(run => run.Language).HasMaxLength(5).IsRequired();
        builder.Property(run => run.Prompt).HasMaxLength(AiDemoRun.PromptMaxLength).IsRequired();
        builder.Property(run => run.Output).HasMaxLength(AiDemoRun.OutputMaxLength);
        builder.Property(run => run.FailureCode).HasMaxLength(AiDemoRun.FailureCodeMaxLength);
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasOne<CustomerAccount>().WithMany().HasForeignKey(run => run.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SaasProduct>().WithMany().HasForeignKey(run => run.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(run => new { run.CustomerId, run.CreatedAtUtc });
        builder.HasIndex(run => new { run.Status, run.CreatedAtUtc });
    }
}
