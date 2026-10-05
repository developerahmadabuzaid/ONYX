using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Onyx.Application.Abstractions.Messaging;
using Onyx.Application.Notifications;
using Onyx.Application.Providers;

namespace Onyx.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IProviderConnectionService, ProviderConnectionService>();
        services.AddScoped<IProviderConnectionAdministrationService, ProviderConnectionAdministrationService>();
        services.AddScoped<Identity.ICurrentAdmin, Identity.CurrentAdmin>();
        services.AddScoped<Privacy.ICookieConsentService, Privacy.CookieConsentService>();

        AddContent(services);
        AddLeads(services);
        services.AddScoped<Email.IEmailTemplateAdministrationService, Email.EmailTemplateAdministrationService>();

        AddDomainEventHandlers(services, assembly);
        return services;
    }

    /// <summary>CMS, media, brand kit, approved knowledge and content intelligence (Stage 6). Settings are registered by the host.</summary>
    private static void AddContent(IServiceCollection services)
    {
        services.AddScoped<Content.ContentAuthorizer>();
        services.AddScoped<Content.ContentAccess>();
        services.AddScoped<Content.ContentRevisionRecorder>();
        services.AddScoped<Content.ContentPathService>();
        services.AddScoped<Content.ContentQualityGate>();
        services.AddScoped<Content.ContentPublisher>();
        services.AddScoped<Content.CtaResolver>();
        services.AddScoped<Content.PublicContentBuilder>();
        services.AddScoped<Media.MediaUsageIndexer>();
        services.AddScoped<Seo.IOrganizationProfileService, Seo.OrganizationProfileService>();
        services.AddScoped<Proof.IContentProofService, Proof.ContentProofService>();
        services.AddScoped<Proof.IProofItemService, Proof.ProofItemService>();
        services.AddScoped<Insights.IAuthorService, Insights.AuthorService>();

        services.AddScoped<Content.IContentAdministrationService, Content.ContentAdministrationService>();
        services.AddScoped<Content.IContentWorkflowService, Content.ContentWorkflowService>();
        services.AddScoped<Content.IContentRevisionService, Content.ContentRevisionService>();
        services.AddScoped<Content.IContentPreviewService, Content.ContentPreviewService>();
        services.AddScoped<Content.IPublicContentService, Content.PublicContentService>();
        services.AddScoped<Content.IContentSchedulingService, Content.ContentSchedulingService>();
        services.AddScoped<Content.IRedirectAdministrationService, Content.RedirectAdministrationService>();
        services.AddScoped<Content.ICallToActionService, Content.CallToActionService>();
        services.AddScoped<Content.IContentClusterService, Content.ContentClusterService>();

        services.AddScoped<Media.IMediaService, Media.MediaService>();
        services.AddScoped<Brand.IBrandKitService, Brand.BrandKitService>();
        services.AddScoped<Knowledge.IKnowledgeService, Knowledge.KnowledgeService>();
        services.AddScoped<Knowledge.CompanyKnowledgeSource>();

        services.AddScoped<Intelligence.IPublishingTimeAdvisor, Intelligence.PublishingTimeAdvisor>();
        services.AddScoped<Intelligence.IIntelligenceStatusService, Intelligence.IntelligenceStatusService>();
        services.AddScoped<Intelligence.IKeywordMapService, Intelligence.KeywordMapService>();
        services.AddScoped<Intelligence.ICompetitorService, Intelligence.CompetitorService>();
        services.AddScoped<Intelligence.ISeoRecommendationService, Intelligence.SeoRecommendationService>();
        services.AddScoped<Intelligence.IInternalLinkAnalyzer, Intelligence.InternalLinkAnalyzer>();
        services.AddScoped<Intelligence.IContentCalendarService, Intelligence.ContentCalendarService>();
    }

    /// <summary>Stage 8: inquiries, leads and consultation booking. <see cref="Leads.LeadSettings"/> is registered by the host.</summary>
    private static void AddLeads(IServiceCollection services)
    {
        services.AddScoped<Leads.InquiryIntake>();
        services.AddScoped<Leads.InquiryRecorder>();
        services.AddScoped<Leads.LeadNotifier>();
        services.AddScoped<Leads.IInquiryService, Leads.InquiryService>();
        services.AddScoped<Leads.ILeadAdministrationService, Leads.LeadAdministrationService>();

        services.AddScoped<Booking.BookingMessages>();
        services.AddScoped<Booking.IAvailabilityProvider, Booking.InternalAvailabilityProvider>();
        services.AddScoped<Booking.IBookingCalendarProvider, Booking.InternalBookingCalendarProvider>();
        services.AddScoped<Booking.IBookingService, Booking.BookingService>();
        services.AddScoped<Booking.IBookingAdministrationService, Booking.BookingAdministrationService>();
        services.AddScoped<Booking.IAvailabilityAdministrationService, Booking.AvailabilityAdministrationService>();
    }

    private static void AddDomainEventHandlers(IServiceCollection services, System.Reflection.Assembly assembly)
    {
        var registrations = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .Select(contract => (Contract: contract, Implementation: type)));

        foreach (var (contract, implementation) in registrations)
        {
            services.AddScoped(contract, implementation);
        }
    }
}
