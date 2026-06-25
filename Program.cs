using System.Globalization;
using BudgetTracker;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Start every run from the same rigged demo data.
Database.Reset();

// Serve the dashboard (wwwroot/index.html) at "/".
app.UseDefaultFiles();
app.UseStaticFiles();

// List every transaction — the basic surface the app ships with.
app.MapGet("/transactions", () =>
{
    using var conn = Database.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText =
        """
        SELECT t.id, a.name, c.name, t.amount_cents, t.description, t.occurred_on, t.is_pending
        FROM transactions t
        JOIN accounts a        ON a.id = t.account_id
        LEFT JOIN categories c ON c.id = t.category_id
        ORDER BY t.occurred_on, t.id
        """;
    using var r = cmd.ExecuteReader();
    var rows = new List<object>();
    while (r.Read())
    {
        rows.Add(new
        {
            id = r.GetInt64(0),
            account = r.GetString(1),
            category = r.IsDBNull(2) ? null : r.GetString(2),
            amount = Money.Format(r.GetInt64(3)),
            description = r.GetString(4),
            date = r.GetString(5),
            pending = r.GetInt64(6) == 1,
        });
    }
    return Results.Ok(rows);
});

// "How much did I spend this month?"
//
// Intentionally NAIVE: sums only negative amounts, across every account, including pending.
// So it counts a transfer to your own savings as spending, ignores a refund, and includes a
// charge that hasn't cleared. It returns $1,300; the honest answer is $850. See README.md.
app.MapGet("/summary", (string? month) =>
{
    month ??= DateTime.Today.ToString("yyyy-MM");
    var start = $"{month}-01";
    var end = DateTime.Parse(start, CultureInfo.InvariantCulture)
        .AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");

    using var conn = Database.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText =
        """
        SELECT COALESCE(-SUM(amount_cents), 0)
        FROM transactions
        WHERE amount_cents < 0
          AND occurred_on BETWEEN $start AND $end
        """;
    cmd.Parameters.AddWithValue("$start", start);
    cmd.Parameters.AddWithValue("$end", end);
    var spentCents = Convert.ToInt64(cmd.ExecuteScalar());

    return Results.Ok(new
    {
        month,
        totalSpent = Money.Format(spentCents),
        totalSpentCents = spentCents,
    });
});

// Record a transaction. The dashboard's "add" form posts here.
app.MapPost("/transactions", (NewTransaction input) =>
{
    if (string.IsNullOrWhiteSpace(input.Description) || input.AmountCents == 0)
        return Results.BadRequest(new { error = "description and a non-zero amountCents are required" });

    using var conn = Database.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText =
        """
        INSERT INTO transactions (account_id, category_id, amount_cents, description, occurred_on, is_pending)
        VALUES (1, $cat, $amt, $desc, $date, $pending)
        """;
    cmd.Parameters.AddWithValue("$cat", (object?)input.CategoryId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("$amt", input.AmountCents);
    cmd.Parameters.AddWithValue("$desc", input.Description.Trim());
    cmd.Parameters.AddWithValue("$date", DateTime.Today.ToString("yyyy-MM-dd"));
    cmd.Parameters.AddWithValue("$pending", input.IsPending ? 1 : 0);
    cmd.ExecuteNonQuery();
    return Results.Created("/transactions", null);
});

// Mark a pending transaction as cleared. The dashboard's "Mark cleared" button calls this.
app.MapPost("/transactions/{id}/clear", (long id) =>
{
    using var conn = Database.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "UPDATE transactions SET is_pending = 0 WHERE id = $id";
    cmd.Parameters.AddWithValue("$id", id);
    return cmd.ExecuteNonQuery() == 0 ? Results.NotFound() : Results.NoContent();
});

app.Run();

// Exposed so the test project's WebApplicationFactory<Program> can boot the app in-memory.
public partial class Program { }

// Body of POST /transactions (the dashboard's add form).
record NewTransaction(long AmountCents, string Description, long? CategoryId, bool IsPending);
