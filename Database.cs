using Microsoft.Data.Sqlite;

namespace BudgetTracker;

public static class Database
{
    public const string ConnectionString = "Data Source=budget.db";

    public static SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Rebuilds the demo database from scratch on every startup so the workshop always begins
    /// from the identical, deliberately-rigged state. (You would never do this in production.)
    /// </summary>
    public static void Reset()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists("budget.db"))
            File.Delete("budget.db");

        var thisMonth = DateTime.Today.ToString("yyyy-MM-dd");
        var lastMonth = DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd");

        using var conn = Open();

        var schema = conn.CreateCommand();
        schema.CommandText =
            """
            CREATE TABLE accounts   (id INTEGER PRIMARY KEY, name TEXT NOT NULL);
            CREATE TABLE categories (id INTEGER PRIMARY KEY, name TEXT NOT NULL);
            CREATE TABLE transactions (
                id           INTEGER PRIMARY KEY,
                account_id   INTEGER NOT NULL,
                category_id  INTEGER,            -- NULL = uncategorized
                amount_cents INTEGER NOT NULL,   -- positive = money in, negative = money out
                kind         TEXT    NOT NULL,   -- Income | Expense | Transfer
                description  TEXT    NOT NULL,
                occurred_on  TEXT    NOT NULL,   -- ISO yyyy-MM-dd
                is_pending   INTEGER NOT NULL    -- 1 = hasn't cleared yet
            );

            INSERT INTO accounts   (id, name) VALUES (1, 'Checking'), (2, 'Savings');
            INSERT INTO categories (id, name) VALUES (1, 'Rent'), (2, 'Groceries');
            """;
        schema.ExecuteNonQuery();

        // Rigged seed. A naive "spent this month" reads $1,300; the honest figure is $850.
        // The gap is three buried decisions: a $200 transfer (not spending), a $100 refund
        // (should reduce spending), and a $150 pending charge (hasn't cleared). See README.
        var seed = conn.CreateCommand();
        seed.CommandText =
            $"""
            INSERT INTO transactions
                (account_id, category_id, amount_cents, kind, description, occurred_on, is_pending)
            VALUES
                (1, 1,    -60000, 'Expense',  'Monthly rent',           '{thisMonth}', 0),
                (1, 2,    -30000, 'Expense',  'Grocery run',            '{thisMonth}', 0),
                (1, 2,     10000, 'Expense',  'Grocery refund',         '{thisMonth}', 0),
                (1, NULL, -20000, 'Transfer', 'Transfer to savings',    '{thisMonth}', 0),
                (2, NULL,  20000, 'Transfer', 'Transfer from checking', '{thisMonth}', 0),
                (1, 2,    -15000, 'Expense',  'Pending groceries',      '{thisMonth}', 1),
                (1, NULL,  -5000, 'Expense',  'ATM withdrawal',         '{thisMonth}', 0),
                (1, 1,    -60000, 'Expense',  'Last month rent',        '{lastMonth}', 0);
            """;
        seed.ExecuteNonQuery();
    }
}
