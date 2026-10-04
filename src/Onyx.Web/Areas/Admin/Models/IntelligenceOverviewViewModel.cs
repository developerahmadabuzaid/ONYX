using Onyx.Contracts.Intelligence;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record IntelligenceOverviewViewModel(IntelligenceCapabilitiesResponse Capabilities, InternalLinkReportResponse LinkReport);
