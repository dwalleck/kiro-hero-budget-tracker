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

    // Spending now excludes transfers and pending, and nets refunds: $850, not the naive $1,300.
    [Fact]
    public async Task Summary_excludes_transfers_refunds_and_pending()
    {
        var client = _factory.CreateClient();
        var summary = await client.GetFromJsonAsync<JsonElement>("/summary");
        Assert.Equal(85000, summary.GetProperty("totalSpentCents").GetInt64());
    }

    [Fact]
    public async Task Transactions_expose_a_kind_including_transfers()
    {
        var client = _factory.CreateClient();
        var rows = await client.GetFromJsonAsync<List<JsonElement>>("/transactions");
        Assert.Contains(rows!, r => r.GetProperty("kind").GetString() == "Transfer");
    }
}
