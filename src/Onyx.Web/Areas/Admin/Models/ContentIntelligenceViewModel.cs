using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record ContentIntelligenceViewModel(ContentDetailResponse Content, ContentIntelligenceResponse Intelligence);
