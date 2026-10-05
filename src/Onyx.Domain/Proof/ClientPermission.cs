namespace Onyx.Domain.Proof;

/// <summary>Whether the client agreed to be identified (name, logo, quote). Testimonials always need it.</summary>
public enum ClientPermission
{
    NotApplicable = 0,
    Granted = 1,
}
