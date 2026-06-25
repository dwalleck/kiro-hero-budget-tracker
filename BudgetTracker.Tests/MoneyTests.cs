using BudgetTracker;

namespace BudgetTracker.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(60000, "$600.00")]
    [InlineData(130000, "$1,300.00")]
    [InlineData(0, "$0.00")]
    public void Format_renders_cents_as_dollars(long cents, string expected)
        => Assert.Equal(expected, Money.Format(cents));
}
