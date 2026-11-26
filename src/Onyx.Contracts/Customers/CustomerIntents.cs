namespace Onyx.Contracts.Customers;

/// <summary>What a visitor wanted to do on a product page; carried through sign-in and sign-up. Anything else is ignored.</summary>
public static class CustomerIntents
{
    public const string Trial = "trial";
    public const string Subscribe = "subscribe";
    public const string Demo = "demo";

    public static bool IsValid(string? intent) => intent is Trial or Subscribe or Demo;
}
