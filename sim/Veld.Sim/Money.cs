using System.Collections.Generic;

namespace Veld.Sim;

/// <summary>
/// Money. In 1901 it is pre-decimal: 12 pence to a shilling, 20 shillings to a pound, stored as pence.
/// In the modern era the same integer is cents and prints as rands.
/// </summary>
public static class Money
{
    public const int Pound = 240;
    public const int Shilling = 12;
    public static bool Modern { get; set; }

    public static string Format(int amount)
    {
        bool neg = amount < 0;
        amount = System.Math.Abs(amount);
        if (Modern) return (neg ? "-" : "") + $"R{amount / 100}.{amount % 100:00}";
        int pounds = amount / Pound;
        int shillings = (amount % Pound) / Shilling;
        int d = amount % Shilling;
        var parts = new List<string>();
        if (pounds > 0) parts.Add($"£{pounds}");
        if (shillings > 0) parts.Add($"{shillings}s");
        if (d > 0 || parts.Count == 0) parts.Add($"{d}d");
        return (neg ? "-" : "") + string.Join(" ", parts);
    }
}
