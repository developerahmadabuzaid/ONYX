namespace Onyx.Domain.Common;

/// <summary>The property's value must never appear in audit logs or application logs; only the fact that it changed is recorded.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class SensitiveDataAttribute : Attribute;
