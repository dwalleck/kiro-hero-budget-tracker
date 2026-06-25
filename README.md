# Budget Tracker (workshop starter app)

The carried app for the *Zero to Kiro Hero* workshop. You didn't write it, and that's the point.

## What it does

It's a tiny budget tracker. You record transactions (money going out, money coming in), and it
tells you one thing: **how much you spent this month.** There's a small web dashboard with the
running total, the list of transactions, and a form to add more.

It's deliberately small: a .NET minimal API plus SQLite, one project, no architecture ceremony.
The interesting part is that the "how much did I spend" number is computed naively, so it's wrong
in ways that look right. That wrongness is what the workshop is about.

## Run it

```bash
dotnet run --urls http://localhost:5000
```

Then open `http://localhost:5000` in a browser. You'll see "Spent this month" up top, the
transactions below it, and a form to add one. The sample data is rebuilt on every startup, so it
always begins from the same numbers. Anything you add is your own sandbox until the next restart.

There are three endpoints behind the page if you'd rather poke at it directly:

- `GET /summary` returns the spending total.
- `GET /transactions` returns the list.
- `POST /transactions` adds one (this is what the form calls).

## The "is it right?" number

The dashboard says you spent **$1,300.00** this month. It's wrong. The honest figure is
**$850.00**. The app ships with an intentionally naive calculation: sum every negative amount this
month, across every account, including charges that haven't cleared. That's exactly the
plausible-looking code an agent writes on the first try.

| What naive counts | Amount | Should it be spending? |
|---|---|---|
| Rent | $600 | yes |
| Groceries | $300 | yes (but see refund) |
| Grocery refund | ignored (+$100) | no, it should reduce groceries to $200 |
| Transfer to savings | $200 | no, you moved it to your own account |
| Pending groceries | $150 | not yet, it hasn't cleared |
| ATM withdrawal (uncategorized) | $50 | yes |
| Last month's rent | excluded by date | n/a |

`$1,300 - $200 transfer - $100 refund - $150 pending = $850`

You can watch the bug happen live: in the dashboard, add a "money out" transaction called
"Transfer to savings" and the total climbs, even though you didn't actually spend it.

## The independent oracle

Check the honest number a different way, with raw SQL against the same database
(`sqlite3 budget.db`). This is the "prove it" check for the cold open and for Act 3:

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

The questions a spec gate or `grill-me-with-docs` should force you to answer:

- **Refunds.** Does a refund reduce spending? (naive: no)
- **Transfers.** Does moving money between your own accounts count? (naive: yes, and the matching
  `+$200` row also inflates income)
- **Pending.** Do uncleared charges count? (naive: yes)
- **Uncategorized.** The `$50` shows in the total but would vanish from a per-category breakdown
  (`category_id IS NULL`).
- **Money.** Amounts are integer **cents** (`amount_cents`), never float, and formatting lives only
  in `Money.Format`. (The Act 1 steering lesson enforces this.)

## The Act 3 closed set

Transactions are classified income vs. expense purely by the **sign** of the amount. There is no
`Transfer` concept, which is exactly why transfers are miscounted. The Act 3 brownfield change is
to add a `Transfer` kind to that closed set. Done right it threads through several places: the
schema, the seed, the `/summary` query, the `/transactions` shape, the add form, and the tests.
Vibing patches one spot and misses the rest. Planning enumerates them first.

## Files

- `Program.cs` is the endpoints (`/summary`, `/transactions` GET and POST).
- `Database.cs` is the schema and the rigged seed.
- `Money.cs` is integer-cents formatting, in one place.
- `wwwroot/index.html` is the dashboard.
