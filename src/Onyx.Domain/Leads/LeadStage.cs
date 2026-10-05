namespace Onyx.Domain.Leads;

/// <summary>The sales pipeline (architecture.md §13.2). Won and Lost close a lead; a closed lead can be reopened.</summary>
public enum LeadStage
{
    New = 0,
    Contacted = 1,
    Qualified = 2,
    Proposal = 3,
    Won = 4,
    Lost = 5,
}
