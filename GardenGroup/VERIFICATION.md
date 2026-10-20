# Verification and demonstration

Verified on 20 October 2026 using .NET SDK 10.0.102, the .NET/ASP.NET Core 8 runtimes and MongoDB Community Server 8.3 on Windows.

## Existing Garden dataset update

The revised app maps name.firstName/name.lastName, title, and statusInfo from the provided exports. Atlas was checked read-only at 120 employees and 150 tickets. With the user's approval, those exports were imported locally into GardenGroupProjectData, retaining IDs and document data while setting local-only demo password hashes. Ten additional HTTP checks passed for both imported account roles, all 120 real dataset names, the 150-ticket dashboard (49.3% / 32.7% / 18%), missing metadata display, ticket creation/editing and removal of the temporary test record. The earlier 30-check results below describe the previous synthetic dataset.

## Results

- Release solution build: passed, zero warnings and zero errors, with warnings treated as errors.
- Included C# checks: 16 passed.
- Local HTTP integration checks: 30 passed.
- Startup script: first-run seed and application launch verified with its separate local MongoDB instance.
- Browser: login, dashboard and loaded-list filtering verified. Filtering `Critical` showed 5 of the 20 loaded rows; clearing it restored 20 of 20 without navigation.
- PDF report: all four pages rendered and inspected.

The HTTP checks were run first on an isolated verification database and then repeated against the database started by Start-Demo.ps1. They created temporary records and deleted those records after checking the CRUD flows. They did not change the original supplied database. HTTP checks were executed through a temporary local harness; the C# checks and the manual steps below are included in the submission.

## Run the included checks

```powershell
dotnet restore NoSQL_Project.sln
dotnet build NoSQL_Project.sln --configuration Release
dotnet run --project GardenGroup.Checks --configuration Release
```

The 16 checks cover:

1. Closing an unresolved incident leaves its solved date empty.
2. Reopening clears completion dates.
3. Closing a resolved incident preserves its solved date.
4. Repeated close preserves the first closed date.
5. Invalid status is rejected without changing the ticket.
6. Priority descending uses the correct business order.
7. Priority ascending starts with Low.
8. Priority ties use newest date.
9. Empty dashboard returns zero percent.
10. Percentages round to one decimal place.
11. AND search combines all word conditions.
12. OR search combines word alternatives.
13. Regex special characters are escaped.
14. Blank search adds no extra filter.
15. Stored password hash verifies the correct password.
16. Wrong password fails verification.

## HTTP checks performed

1. Anonymous access redirects to login.
2. A missing anti-forgery token is rejected.
3. Invalid login shows a generic error.
4. Service desk dashboard shows the correct seeded counts/percentages.
5. Regular employee dashboard is scoped to their tickets.
6. Regular employee cannot open employee management.
7. A foreign ticket returns 404 for a regular employee.
8. A direct service desk edit URL is restricted.
9. Invalid ObjectId returns 404.
10. A regular employee can create a ticket.
11. Forged reporter, assignee and initial status do not override regular-user restrictions.
12. AND search returns a ticket containing both words.
13. AND excludes tickets missing a word.
14. OR accepts either word.
15. Regex characters are treated literally.
16. Invalid search mode is rejected.
17. Priority sorting applies before pagination.
18. Service desk can edit and resolve a ticket.
19. Regular employee cannot call the close POST action.
20. Transfer rejects a regular-employee recipient.
21. Transfer accepts another service desk employee.
22. Closing a resolved ticket retains its solved outcome.
23. Employee creation allows blank optional contact/location fields.
24. Duplicate email is rejected.
25. Employee edit with blank new password keeps the existing password.
26. Referenced employees cannot be deleted.
27. A service desk employee cannot delete their own account.
28. Confirmed permanent ticket deletion removes that ticket.
29. Unreferenced employee deletion succeeds.
30. Logout removes access to protected pages.

## A short demonstration for class

1. Start the demo and sign in as `donald.allen66@gardengroup.com` using the README password. Explain why only this employee's ticket totals appear.
2. Create a ticket with a subject, description, type, priority, event date and deadline. Show that it starts Open and appears in My tickets.
3. Sign out and sign in as `dorothy.allen11@gardengroup.com`. Show the all-employee dashboard, ticket list and employee menu.
4. Edit the new ticket, assign it and mark it Resolved. Open the details and point out the solved timestamp.
5. Close it and explain why the solved date remains and the ticket belongs to the resolved outcome category.
6. Search for two words using AND, then OR. Explain why search queries MongoDB.
7. Select priority high-to-low. Explain why priority uses a numeric rank rather than alphabetical order.
8. Type `Critical` into Filter this loaded page. Show that rows disappear without a page request, and that clearing it brings them back.
9. Transfer a ticket to another service desk employee from the supplied data using the separate transfer form.
10. Add an employee, edit their details and delete that unreferenced demo account. Do not use real employee data.
11. Open Delivery 1 and connect its document model to DATABASE.md. Explain one embedded object, one reference and the dashboard aggregation.

## What was not verified

- Public deployment or writes to Atlas; Atlas counts were checked read-only.
- Direct login using the original incomplete exported password hashes; the approved local copy uses new demo hashes.
- Exact agreement with a separate classroom UI prototype that was not supplied.
- Real student authorship or a published GitHub URL; these require the group's own records/account.

Large-dataset performance and concurrent-write correctness remain outside this classroom implementation. See DATABASE.md for the specific limitations rather than claiming production readiness.

## Ticket type and priority update

All 150 local tickets now have a valid type and priority. Missing fields were filled using a grouped MongoDB bulk write; any existing nonempty values were preserved. Original source exports and Atlas were not changed. The distributed data/Garden.Ticket.json includes the same suggested classifications, so a fresh local import also has them. Counts: Network 36, Hardware 40, Software 74; Critical 8, High 76, Normal 50, Low 16.

## Subject/description consistency

Corrected 138 mismatched descriptions in the local database and distributed ticket JSON. The 12 already matching descriptions were retained. Updates matched the ticket ID, title and old description, preventing overwriting a later manual edit. Verified all 150 descriptions agree with their titles.

## Open/archive view verification

Thirteen HTTP checks passed for default open-only results, resolved/closed archive results, pagination and search/sort retaining the selected view, both employee roles retaining ownership scope, dashboard totals including both views, closing a temporary ticket into the archive and reopening it into the open view. The temporary ticket was removed after verification. Current user edits were preserved; view totals were checked against the live dashboard rather than the original import counts.
