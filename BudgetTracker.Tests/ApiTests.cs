using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BudgetTracker.Tests;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Transactions_returns_all_seeded_rows()
    {
        var client = _factory.CreateClient();
        var rows = await client.GetFromJsonAsync<List<JsonElement>>("/transactions");
        Assert.Equal(8, rows!.Count);
    }

    // Characterizes the CURRENT (naive) summary. Act 3 deliberately changes this expectation
    // once transfers are excluded — that's the brownfield exercise, not a regression.
    [Fact]
    public async Task Summary_returns_the_current_total()
    {
        var client = _factory.CreateClient();
        var summary = await client.GetFromJsonAsync<JsonElement>("/summary");
        Assert.Equal(130000, summary.GetProperty("totalSpentCents").GetInt64());
    }
}
