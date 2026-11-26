using Microsoft.AspNetCore.Mvc.Razor;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>
/// The planning screens (DeliveryPlanningController) share the delivery workspace's views and partials, so their lookups also
/// search the Delivery view folder. Only that controller is affected.
/// </summary>
public sealed class DeliveryViewLocationExpander : IViewLocationExpander
{
    private const string PlanningController = "DeliveryPlanning";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(viewLocations);
        return string.Equals(context.ControllerName, PlanningController, StringComparison.Ordinal)
            ? viewLocations.Concat(["/Areas/Admin/Views/Delivery/{0}.cshtml"])
            : viewLocations;
    }
}
