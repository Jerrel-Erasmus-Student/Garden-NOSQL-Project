# Garden Group Service Desk

A small ASP.NET Core MVC application using MongoDB. Regular employees report and view their own tickets. Service desk employees manage all tickets and employee accounts.

## What is included

- Login/logout with hashed passwords and role checks.
- Ticket and employee CRUD screens.
- One dashboard view reused for both roles, with different database scopes.
- Search with AND/OR, priority sorting, ticket transfer, and filtering the already loaded ticket page.
- Your supplied MongoDB exports: 120 employees and 150 tickets, with their original names and IDs.
- Delivery 3 report, NoSQL explanation guide, manual demonstration steps, and a small C# verification project.

## Requirements

- .NET SDK 8 or later, with the .NET 8 and ASP.NET Core 8 runtimes.
- MongoDB Community Server (tested with 8.3), or an Atlas connection.
- Internet access for the initial NuGet restore.

## Quick start on Windows

Run `Start-Demo.ps1` from PowerShell. It finds a locally installed MongoDB server, starts a separate instance on port 27184 using `.local-data`, restores the application and starts it at http://localhost:5188. On the first run, use:

```powershell
.\Start-Demo.ps1 -Seed
```

On later runs, omit `-Seed`. The script stops only its own MongoDB process when the application exits. Importing refuses an existing nonempty database. The local database is GardenGroupProjectData; only passwords in this local copy are reset. If PowerShell blocks the script, use the manual commands below rather than changing machine-wide execution settings.

## Manual start

Start MongoDB using your normal local installation. The default connection is `mongodb://127.0.0.1:27017` and the default database is `Garden`.

```powershell
dotnet restore NoSQL_Project.sln
$env:Import__Directory=(Resolve-Path data).Path
dotnet run --project NoSQL_Project -- --import-data --reset-imported-passwords
dotnet run --project NoSQL_Project
```

Open http://localhost:5188. Run the import command only once, against empty Employee and Ticket collections. It never deletes or overwrites existing data. If an import run is interrupted, use a new empty demo database before retrying.

### Demo accounts

| Role | Email | Password |
| --- | --- | --- |
| Service desk | dorothy.allen11@gardengroup.com | GardenDemo!2026 |
| Regular employee | donald.allen66@gardengroup.com | GardenDemo!2026 |


See Employee_Logins.md for all 120 names, emails and roles. The original exported hashes appear incomplete (47-character bcrypt-style values), so the local imported copy uses new ASP.NET password hashes. Atlas and the source JSON files are not changed. These documented passwords are for local demonstration only.

## Your own MongoDB configuration

The original ZIP's `.env` and credentials were deliberately not copied. Use environment variables before starting the app:

```powershell
$env:Mongo__ConnectionString='mongodb://127.0.0.1:27017'
$env:Mongo__Database='YourEmptyDemoDatabase'
$env:Import__Directory=(Resolve-Path data).Path
dotnet run --project NoSQL_Project -- --import-data --reset-imported-passwords
dotnet run --project NoSQL_Project
```

For Atlas, replace the connection string with your own URI, keeping credentials out of Git. Alternatively, use an ignored `NoSQL_Project/appsettings.Local.json` with the same Mongo section as appsettings.json. Environment variables take precedence.

The application now maps the actual export fields: name.firstName/name.lastName, ticket title, and nested statusInfo. IDs and relationships are preserved. The distributed ticket JSON and local copy now include suggested type and priority values based on the incident title. Deadlines remain unset until edited. DATABASE.md records the structure. Direct Atlas access was verified read-only at 120 employees and 150 tickets; the running demonstration uses the approved local copy.

The import command creates the email uniqueness index and ticket indexes. To create/check indexes in an existing compatible database without inserting demo data:

```powershell
dotnet run --project NoSQL_Project -- --ensure-indexes
```

Duplicate email data must be corrected before the unique index can be created.

## Checks

```powershell
dotnet build NoSQL_Project.sln --configuration Release
dotnet run --project GardenGroup.Checks
```

The C# checks do not need a running MongoDB server. See `VERIFICATION.md` for the database/HTTP checks performed and the manual browser demonstration.

## Project layout

- `Models`: MongoDB document classes.
- `Repositories`: employee/ticket queries and CRUD.
- `Controllers`: request handling, role checks and form validation.
- `Services`: separate search, sorting, transfer and ticket-workflow logic; safe JSON import and optional synthetic seeding.
- `ViewModels`: form inputs and screen data; prevents binding password hashes or arbitrary database fields from requests.
- `Views`: MVC Razor screens and shared layout.
- `wwwroot`: CSS and the separate `TicketFilter` JavaScript class.

## GitHub and submission

No GitHub URL was supplied and no repository has been published by this task. Add your real repository URL to the Delivery 3 report after creating/pushing the repository. Do not submit a made-up link.

Create an empty repository in your own GitHub account, then run these commands from this folder, replacing YOUR-USERNAME and YOUR-REPOSITORY:

```powershell
git init
git add .
git commit -m "Complete Garden Group NoSQL application"
git branch -M main
git remote add origin https://github.com/YOUR-USERNAME/YOUR-REPOSITORY.git
git push -u origin main
```

Keep the source ZIP, Delivery 3 report, and repository URL together for submission. Before submitting, the group must fill in the actual contribution record in `Delivery3_Report.md` and regenerate or update the PDF. The current document does not pretend that the named students wrote the AI-assisted additions.

The local/distributed data also corrects 138 mismatched sample ticket descriptions so all 150 descriptions agree with their titles. Original source exports and Atlas are unchanged.

## Open and archived tickets

Tickets opens with only Open incidents. View archived tickets switches to Resolved and Closed incidents; View open tickets returns to active work. Search, sorting, Reset and pagination retain the chosen view. The archive is a status-filtered view of the same Ticket collection, so records stay available and dashboard totals still include them. Closing moves a ticket into this view; reopening brings it back. Both views enforce the employee role/ownership rules.
