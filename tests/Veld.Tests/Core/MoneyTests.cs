using Veld.Core;
using Xunit;

namespace Veld.Tests.Core;

/// <summary>
/// Every Money test lives in this one class: the era is a static flag and xUnit runs classes in
/// parallel, so two classes toggling it would race. Each test sets the flag it needs.
/// </summary>
[Collection("era flags")]
public class MoneyTests
{
    [Theory]
    [InlineData(522, "£2 3s 6d")]
    [InlineData(240, "£1")]
    [InlineData(12, "1s")]
    [InlineData(0, "0d")]
    [InlineData(-30, "-2s 6d")]
    public void Formats_pre_decimal_pounds_shillings_and_pence(int pence, string expected)
    {
        Money.Modern = false;
        Assert.Equal(expected, Money.Format(pence));
    }

    [Theory]
    [InlineData(15000, "R150.00")]
    [InlineData(5, "R0.05")]
    [InlineData(-2000, "-R20.00")]
    public void Formats_rands_and_cents(int cents, string expected)
    {
        Money.Modern = true;
        Assert.Equal(expected, Money.Format(cents));
    }
}
