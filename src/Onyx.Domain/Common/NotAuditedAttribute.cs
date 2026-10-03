namespace Onyx.Domain.Common;

/// <summary>The property is excluded from audit change sets entirely.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class NotAuditedAttribute : Attribute;
