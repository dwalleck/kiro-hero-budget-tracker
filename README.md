# Budget Tracker (workshop starter app)

A deliberately tiny budget API — the carried app for the *Zero to Kiro Hero* workshop.
.NET minimal API + SQLite, one project, no architecture ceremony. You didn't write it, and
that's the point.

Harvested from the `basic-budget` domain: the interesting part — the ways a spending number
can be *subtly wrong* — is preserved; the enterprise scaffolding (CQRS, GraphQL, EF, Aspire)
is not.

## Run it

```bash
dotnet run
# open the http URL printed on startup (e.g. http://localhost:5000), then:
curl "http://localhost:5000/summary"       # the headline number
curl "http://localhost:5000/transactions"  # the underlying data
```

The SQLite file is rebuilt from the rigged seed on every startup, so it always starts identical.

## The rigged "is it right?" number

`GET /summary` answers *"how much did I spend this month?"* and confidently returns **$1,300.00**.

It's wrong. The honest figure is **$850.00**. The app ships with an intentionally naive
calculation — sum every negative amount, across every account, including pending — exactly the
plausible-looking code an agent writes on the first try.

| What naive counts | Amount | Should it be spending? |
|---|---|---|
| Rent | $600 | ✅ yes |
| Groceries | $300 | ✅ yes (but see refund) |
| Grocery **refund** | ignored (+$100) | ➖ should *reduce* groceries to $200 |
| **Transfer** to savings | $200 | ❌ no — moved to your own account |
| **Pending** groceries | $150 | ❌ not yet — hasn't cleared |
| ATM withdrawal (uncategorized) | $50 | ✅ yes |
| Last month's rent | excluded by date | — |

`$1,300 − $200 transfer − $100 refund − $150 pending = $850`

## The independent oracle

Check the honest number a different way — raw SQL against the same DB (`sqlite3 budget.db`).
This is the "prove it" oracle for the cold open and for Act 3:

```sql
-- honest spend this month: net of refunds, no transfers, cleared only  -> 850.00
SELECT printf('$%.2f', -SUM(amount_cents) / 100.0)
FROM transactions
WHERE occurred_on BETWEEN '<first-of-month>' AND '<last-of-month>'
  AND is_pending = 0
  AND description NOT LIKE 'Transfer%';

-- what the app does (naive)                                            -> 1300.00
SELECT printf('$%.2f', -SUM(amount_cents) / 100.0)
FROM transactions
WHERE amount_cents < 0
  AND occurred_on BETWEEN '<first-of-month>' AND '<last-of-month>';
```

## The buried decisions (workshop fuel)

The questions a spec gate / `grill-me-with-docs` should force you to answer:

- **Refunds** — does a refund reduce spending? (naive: no)
- **Transfers** — does moving money between your own accounts count? (naive: yes — and the
  matching `+$200` row also double-inflates income)
- **Pending** — do uncleared charges count? (naive: yes)
- **Uncategorized** — the `$50` shows in the total but would vanish from a per-category
  breakdown (`category_id IS NULL`)
- **Money** — amounts are integer **cents** (`amount_cents`), never float; formatting lives only
  in `Money.Format`. *(The Act 1 steering lesson enforces this convention.)*

## The Act 3 closed set

Transactions are classified income-vs-expense purely by the **sign** of the amount — there is
no `Transfer` concept, which is exactly why transfers are miscounted. The Act 3 brownfield
change is to **add a `Transfer` kind** to that closed set. Done right it threads through
several places — the schema, the seed, the `/summary` query, the `/transactions` shape, and the
tests — and fixing it makes the headline number honest. Vibing patches one spot and misses the
rest; planning enumerates them first.

## Files

- `Program.cs` — endpoints (`/transactions`, `/summary`)
- `Database.cs` — schema + the rigged seed
- `Money.cs` — integer-cents formatting (one place)
