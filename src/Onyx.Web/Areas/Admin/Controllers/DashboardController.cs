using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Models;
using Onyx.Web.Areas.Admin.Navigation;
using Onyx.Web.Authentication;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// The admin overview. Every number is a live total from the API for something the user may see; cards for areas outside the
/// user's permissions are not requested at all, and a failed call drops its card instead of showing a guess.
/// </summary>
[Area("Admin")]
[Authorize]
[Route("admin")]
[TypeFilter<AdminShellFilter>]
public sealed class DashboardController(
    CurrentAdminContext currentAdmin,
    IAdminCmsApiClient cms,
    IAdminLeadsApiClient leads,
    IAdminNewsApiClient news,
    IAdminSiteContentApiClient siteContent,
    IAdminCommerceApiClient commerce,
    IAdminDeliveryApiClient delivery) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var me = await currentAdmin.GetAsync(cancellationToken);
        var permissions = me.Permissions.ToHashSet(StringComparer.Ordinal);
        var cards = new List<DashboardCard>();
        var unavailable = false;
        IReadOnlyList<InquirySummaryResponse>? recent = null;

        // Calls run one after another: they share the session's access token, which may be refreshed (rotated) by the first call.
        void Add<T>(ApiResult<PagedResult<T>> result, string group, string titleKey, string controller, string action, IReadOnlyDictionary<string, string>? route = null, bool attention = false)
        {
            if (result.IsSuccess && result.Value is not null)
            {
                cards.Add(new DashboardCard(group, titleKey, result.Value.TotalCount, controller, action, route, attention && result.Value.TotalCount > 0));
            }
            else
            {
                unavailable = true;
            }
        }

        if (permissions.Contains(Permissions.Pages.View))
        {
            var documents = await siteContent.ListAsync(cancellationToken);
            if (documents.IsSuccess && documents.Value is not null)
            {
                var drafts = documents.Value.Count(document => document.HasDraft);
                cards.Add(new DashboardCard(AdminNavigation.Website, "Dashboard.SiteDrafts", drafts, "SiteContent", "Index", null, drafts > 0));
            }
            else
            {
                unavailable = true;
            }
        }

        if (permissions.Contains(Permissions.Content.View))
        {
            foreach (var (status, titleKey, attention) in new[] { ("Draft", "Dashboard.ContentDrafts", false), ("PendingReview", "Dashboard.ContentReview", true), ("Scheduled", "Dashboard.ContentScheduled", false), ("Published", "Dashboard.ContentPublished", false) })
            {
                Add(await cms.ListContentAsync(null, status, false, null, 1, cancellationToken), AdminNavigation.ContentGroup, titleKey, "Content", "Index", new Dictionary<string, string> { ["status"] = status }, attention);
            }
        }

        if (permissions.Contains(Permissions.Media.View))
        {
            Add(await cms.ListMediaAsync(null, 1, cancellationToken), AdminNavigation.Website, "Dashboard.Media", "MediaLibrary", "Index");
        }

        if (permissions.Contains(Permissions.News.View))
        {
            Add(await news.ListItemsAsync("Held", null, 1, cancellationToken), AdminNavigation.NewsGroup, "Dashboard.NewsHeld", "NewsItems", "Index", new Dictionary<string, string> { ["status"] = "Held" }, attention: true);
        }

        if (permissions.Contains(Permissions.Leads.View))
        {
            Add(await leads.ListLeadsAsync(new LeadListQuery(Stage: "New", PageSize: 1), cancellationToken), AdminNavigation.LeadsGroup, "Dashboard.LeadsNew", "Leads", "Index", new Dictionary<string, string> { ["stage"] = "New" }, attention: true);
            Add(await leads.ListLeadsAsync(new LeadListQuery(PageSize: 1), cancellationToken), AdminNavigation.LeadsGroup, "Dashboard.LeadsAll", "Leads", "Index");
        }

        if (permissions.Contains(Permissions.Inquiries.View))
        {
            var inquiries = await leads.ListInquiriesAsync(new InquiryListQuery(PageSize: 5), cancellationToken);
            Add(inquiries, AdminNavigation.LeadsGroup, "Dashboard.Inquiries", "Inquiries", "Index");
            recent = inquiries.IsSuccess ? inquiries.Value?.Items : null;
        }

        if (permissions.Contains(Permissions.Bookings.View))
        {
            Add(await leads.ListBookingsAsync(new BookingListQuery(Upcoming: true, PageSize: 1), cancellationToken), AdminNavigation.LeadsGroup, "Dashboard.BookingsUpcoming", "Bookings", "Index", attention: true);
        }

        if (permissions.Contains(Permissions.Products.View))
        {
            var products = await commerce.ProductsAsync("en", cancellationToken);
            if (products.IsSuccess && products.Value is not null)
            {
                var drafts = products.Value.Count(product => product.Status == "Draft");
                cards.Add(new DashboardCard(AdminNavigation.CommerceGroup, "Dashboard.ProductsPublished", products.Value.Count(product => product.Status == "Published"), "Commerce", "Products"));
                cards.Add(new DashboardCard(AdminNavigation.CommerceGroup, "Dashboard.ProductsDraft", drafts, "Commerce", "Products", null, drafts > 0));
            }
            else
            {
                unavailable = true;
            }
        }

        if (permissions.Contains(Permissions.Customers.View))
        {
            Add(await commerce.CustomersAsync(null, "Active", 1, cancellationToken), AdminNavigation.CommerceGroup, "Dashboard.Customers", "Commerce", "Customers",
                new Dictionary<string, string> { ["status"] = "Active" });
        }

        if (permissions.Contains(Permissions.Subscriptions.View))
        {
            foreach (var (status, titleKey, attention) in new[] { ("Requested", "Dashboard.SubscriptionRequests", true), ("Trial", "Dashboard.SubscriptionTrials", false), ("Active", "Dashboard.SubscriptionsActive", false) })
            {
                Add(await commerce.SubscriptionsAsync(status, "en", 1, cancellationToken), AdminNavigation.CommerceGroup, titleKey, "Commerce", "Subscriptions",
                    new Dictionary<string, string> { ["status"] = status }, attention);
            }
        }

        if (permissions.Contains(Permissions.Invoices.View))
        {
            Add(await commerce.InvoicesAsync("Issued", null, 1, cancellationToken), AdminNavigation.CommerceGroup, "Dashboard.InvoicesOutstanding", "Commerce", "Invoices",
                new Dictionary<string, string> { ["status"] = "Issued" }, attention: true);
        }

        if (permissions.Contains(Permissions.Delivery.Access))
        {
            var work = await delivery.DashboardAsync(cancellationToken);
            if (work.IsSuccess && work.Value is { } board)
            {
                cards.Add(new DashboardCard(AdminNavigation.DeliveryGroup, "Dashboard.ActiveProjects", board.ActiveProjects, "Delivery", "Projects"));
                cards.Add(new DashboardCard(AdminNavigation.DeliveryGroup, "Dashboard.OpenIssues", board.OpenIssues, "Delivery", "Issues"));
                cards.Add(new DashboardCard(AdminNavigation.DeliveryGroup, "Dashboard.MyIssues", board.AssignedToMe, "Delivery", "MyWork"));
                cards.Add(new DashboardCard(AdminNavigation.DeliveryGroup, "Dashboard.OverdueIssues", board.Overdue, "Delivery", "Issues", null, board.Overdue > 0));
                cards.Add(new DashboardCard(AdminNavigation.DeliveryGroup, "Dashboard.CompletedThisWeek", board.CompletedThisWeek, "Delivery", "Index"));
            }
            else
            {
                unavailable = true;
            }
        }

        return View(new DashboardModel(me.DisplayName, cards, recent, unavailable, permissions));
    }
}
