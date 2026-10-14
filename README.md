# SEBMS

[![CI](https://github.com/iamakshukla/SEBMS/actions/workflows/ci.yml/badge.svg)](https://github.com/iamakshukla/SEBMS/actions/workflows/ci.yml)

Smart Expense & Budget Management System — track income and expenses, set monthly budgets per category, and see where your money goes.

Built with ASP.NET Core (.NET 10): a JSON Web API secured with JWT, an MVC web app on top of it, and SQL Server via EF Core.

## Features

- **Accounts** — register, sign in, sign out (ASP.NET Core Identity + JWT).
- **Transactions** — add, edit, delete (soft delete), filter by date range, type and category, **export to CSV**.
- **Recurring transactions** — set up rent, salary or subscriptions once (weekly, monthly or yearly); each occurrence is recorded automatically and marked "Recurring" in the transaction list. Pause, resume, edit or delete a rule at any time.
- **Categories** — 13 built-in defaults (read-only) plus your own custom categories.
- **Budgets** — monthly limit per expense category with live *spent* and *remaining* amounts. **Budget alerts** appear once a budget is 80% used and again when it is exceeded: right after you save an expense, in a banner on the dashboard, and as badges on the Budgets page.
- **Dashboard** — monthly income, expenses and net balance, with charts for spending by category, budget vs spent and a 12-month overview.
- **Monthly report** — download any month as a **PDF** or an **Excel** workbook (summary, category breakdown, budgets and every transaction) from the dashboard buttons, or at `/Reports/Monthly?year=2026&month=10&format=pdf|xlsx`.
- Every user only ever sees their own data (except the admin, see below).

Report libraries: [ClosedXML](https://github.com/ClosedXML/ClosedXML) (MIT) for Excel and [QuestPDF](https://www.questpdf.com) for PDF. QuestPDF is used under its free Community licence (individuals and small organisations); check its licence terms before using this commercially at larger scale.

## Solution layout

```
src/
  SEBMS.Domain          Entities (Category, Transaction, Budget) and enums
  SEBMS.Application     DTOs (requests/responses with validation rules)
  SEBMS.Infrastructure  EF Core DbContext, configurations, migrations, Identity user
  SEBMS.Api             REST API: controllers, JWT, Swagger
  SEBMS.Web             MVC web app (calls the API; keeps the JWT in an HttpOnly cookie)
tests/
  SEBMS.Tests           xUnit tests (in-memory EF database)
database/               Original draft SQL script, kept for reference only
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server — Express, LocalDB or full (the examples use `localhost\SQLEXPRESS`)
- EF Core CLI: `dotnet tool install --global dotnet-ef`
- Visual Studio 2022 17.14+ (optional)

## First-time setup

Run these from the repository root.

**1. Store the API's secrets** (kept outside the repo in your user profile):

```powershell
cd src\SEBMS.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\SQLEXPRESS;Database=SEBMS;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet user-secrets set "Jwt:Key"      "<a random string of at least 32 characters>"
dotnet user-secrets set "Jwt:Issuer"   "SEBMS"
dotnet user-secrets set "Jwt:Audience" "SEBMS.Clients"
dotnet user-secrets set "Admin:Email" "<the email of the one admin account>"
cd ..\..
```

Generate a key with PowerShell: `[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))`

**2. Create the database** (applies the EF migrations and seeds the default categories):

```powershell
dotnet ef database update --project src\SEBMS.Infrastructure --startup-project src\SEBMS.Api
```

**3. Check the Web app can find the API.** `src\SEBMS.Web\appsettings.json` has:

```json
"Api": { "BaseUrl": "http://localhost:5076/" }
```

Change it only if you run the API on a different address.

## Running

The Web app needs the API running, so start **both**.

**Command line** — two terminals:

```powershell
dotnet run --project src\SEBMS.Api --launch-profile https
dotnet run --project src\SEBMS.Web --launch-profile https
```

**Visual Studio** — right-click the solution → *Configure Startup Projects* → *Multiple startup projects* → set `SEBMS.Api` and `SEBMS.Web` to **Start**, then press F5.

| App | URL |
| --- | --- |
| Web app | https://localhost:7062 (or http://localhost:5294) |
| API + Swagger UI | https://localhost:7194/swagger (or http://localhost:5076/swagger) |

Open the web app, click **Register**, and you're in. Passwords need at least 8 characters with upper and lower case letters, a digit and a symbol (for example `Password1!`).

### Trying the API directly in Swagger

1. `POST /api/auth/register`, then `POST /api/auth/login`, and copy the `token` from the response.
2. Click **Authorize** and paste the token (without the word "Bearer").
3. Call any endpoint.

## API overview

All endpoints except `auth/*` require `Authorization: Bearer <token>`.

| Endpoint | Description |
| --- | --- |
| `POST /api/auth/register`, `POST /api/auth/login` | Create an account / get a token |
| `GET/POST /api/categories`, `GET/PUT/DELETE /api/categories/{id}` | Default + own categories (defaults are read-only) |
| `GET/POST /api/transactions`, `GET/PUT/DELETE /api/transactions/{id}` | Filters: `from`, `to`, `categoryId`, `type` |
| `GET/POST /api/budgets`, `GET/PUT/DELETE /api/budgets/{id}` | Filters: `year`, `month`; includes `spent` and `remaining` |
| `GET/POST /api/recurring`, `GET/PUT/DELETE /api/recurring/{id}`, `POST /api/recurring/run` | Recurring rules (`Weekly`/`Monthly`/`Yearly`); `run` generates anything currently due |
| `GET /api/reports/summary?year=&month=` | Income, expense and net for a month |
| `GET /api/reports/by-category?year=&month=&type=` | Spending (or income) per category with percentages |
| `GET /api/reports/monthly-trend?year=` | All 12 months of income and expense |

Business rules worth knowing:

- A transaction's type must match its category's type.
- Budgets are only allowed for expense categories, one per category per month.
- A category that has transactions, budgets or recurring transactions cannot be deleted.
- A recurring transaction creates a real transaction on each due date (weekly, monthly or yearly, with an optional end date). The API generates them when a rule is saved, at start-up (catching up on anything missed while it was off) and then every hour. A monthly rule on the 31st falls on the last day of shorter months and returns to the 31st afterwards. Deleting a rule keeps the transactions it already created. Changing a rule's start date or frequency restarts it without re-creating past dates.
- Deleted transactions are hidden, not removed (soft delete).

## Tests

```powershell
dotnet test
```

The tests use an in-memory database, so they don't touch your real data.

The same restore, build and test steps run automatically on GitHub for every push to `main` and every pull request (`.github/workflows/ci.yml`). The badge at the top shows the latest result.

## Troubleshooting

| Problem | Fix |
| --- | --- |
| Build error: *file is being used by another process* / *Unable to copy file ... SEBMS.Api* | A running copy of the app is locking the DLLs. Stop it (Shift+F5, or Ctrl+C in its terminal) and build again. |
| Web app says *The API is not reachable* | Start `SEBMS.Api`, and check `Api:BaseUrl` in `SEBMS.Web\appsettings.json`. |
| Registration returns 400 | Read the message: weak password (see the rules above) or the email is already registered. |
| Signed out right after logging in | The API session token was rejected — usually `Jwt:Key` changed or is missing in user-secrets. Set it, restart the API and sign in again. |
| API fails at startup with a missing key or connection string | Redo step 1 of the setup. |
| Cannot connect to SQL Server | Check the server name in the connection string (`localhost\SQLEXPRESS` vs `(localdb)\MSSQLLocalDB`) and that the SQL Server service is running. |
| Browser warns about the HTTPS certificate | Run `dotnet dev-certs https --trust` once. |

## Admin

The account whose email equals `Admin:Email` is the application's admin. After signing in with that email, an **Admin** link appears in the navbar. The admin can list all users, delete a user together with all their data, and view every user's transactions, budgets and categories. The admin can also **edit or delete** any user's transactions, budgets and custom categories from the Admin tables. Edits follow the *owner's* rules: a transaction can only use categories that user can see, a user still gets only one budget per category and month, and the shared default categories stay read-only for everyone. Deleting a transaction is a soft delete, like when the owner does it, and a category that is in use cannot be deleted. Nobody else can get the role: it is re-checked against the setting on every sign-in, and the API enforces it on `/api/admin/*`.

**Audit log.** Every successful admin change (editing or deleting a transaction, budget or category, and deleting a user) is recorded in the `AuditLogs` table, in the same database step as the change itself. Each entry stores when, which admin, which user's data, and what changed, for example `Amount: 800.00 → 950.50`, or a snapshot of what was deleted. Edits that are rejected, or that change nothing, are not recorded. Read it under **Admin → Audit log** (newest first, 50 per page; the **History** button on a user filters to that user). The log is append-only through the application (the API only offers a read endpoint), and entries keep the emails as text so they stay readable after a user is deleted. The affected user is not notified.

## Configuration reference

| Setting | Where | Purpose |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | API user-secrets | SQL Server connection |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` | API user-secrets | Token signing and validation (tokens last 8 hours) |
| `Admin:Email` | API user-secrets | The one admin account (see Admin). Leave unset for no admin |
| `Api:BaseUrl` | `SEBMS.Web\appsettings.json` | Where the web app finds the API |
