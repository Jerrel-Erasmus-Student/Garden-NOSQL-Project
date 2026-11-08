# Garden Group - Delivery 3
## Application Components and Design Choices

**Group members:** Ahmed, Denys, Maksym and Jerrel  
**GitHub:** https://github.com/Jerrel-Erasmus-Student/Garden-NOSQL-Project

## Application overview

The application lets Garden Group employees report problems and follow their tickets. Service desk employees can manage all tickets and employee accounts. Both roles have a dashboard showing open, resolved and closed-without-resolution tickets.

The project uses C#, ASP.NET Core MVC and MongoDB. It builds on the database model from Delivery 1 and the basic CRUD methods from the starting project. Controllers handle requests and validation, repositories contain database queries, and Razor views display the results.

## Work allocation

| Member | Main components | Allowed individual feature | Separate class |
| --- | --- | --- | --- |
| Ahmed | Employee management and account validation | Sort tickets by priority | `PrioritySortService` (C#) |
| Denys | Ticket management and status changes | Transfer a ticket to another person | `TicketTransferService` (C#) |
| Maksym | Interface, navigation and application setup | Filter a preloaded ticket list | `TicketFilter` (JavaScript) |
| Jerrel | Login, permissions and dashboard | Search subject/content with AND, OR and newest-first results | `TicketSearchService` (C#) |

The four individual features are different options from the supplied allowed-functionality list, and each has its own class. The Open/Archived page toggle is part of normal ticket handling; it is not counted as the additional feature that moves older records to a secondary archive database.

## Ahmed - Employee management and priority sorting

Ahmed's part covers employee accounts: the list, create/edit forms, validation and deletion rules. `EmployeesController` checks the input and calls `EmployeeRepository` to save it. New accounts need a password; a blank password during an edit keeps the current hash. Duplicate emails are rejected.

An employee cannot be deleted while tickets still reference that account. This follows the Employee-Ticket relationship from Delivery 1 and prevents broken reporter or assignee references. Deleting all related tickets would remove useful records, so that approach was not used.

### Individual functionality: sort tickets by priority

**Allowed option:** sorting a ticket list by its priority level.

**Separate class:** `PrioritySortService` in `NoSQL_Project/Services/PrioritySortService.cs`.

The priority levels have a fixed order:

- Critical = 4
- High = 3
- Normal = 2
- Low = 1

The ticket page offers high-to-low and low-to-high sorting. `TicketsController.Index` calls `Sort` when either option is selected. Sorting is applied to the matching tickets before pagination, so a Critical ticket on a later page is not missed. For equal priorities, the newest reported ticket comes first; the document ID breaks any remaining tie.

For example, high-to-low must show Critical, High, Normal, then Low. Sorting the words alphabetically would put Low before Normal, which is why the service uses ranks. The ordering currently runs in C#. Database-side sorting would be a better choice for a much larger dataset.

**How to demonstrate it:** select both sort directions and compare tickets with different priorities. Then compare two equal-priority tickets to check the newest-first tie break.

**Other main files:** `Controllers/EmployeesController.cs`, `Repositories/EmployeeRepository.cs`, `Views/Employees/` and `ViewModels/Forms.cs`, all under `NoSQL_Project`.

## Denys - Ticket handling and transfer

Denys's area follows a ticket through its normal use: an employee reports a problem, the service desk opens its details, and someone updates the status. A new ticket starts Open. For regular employees, the reporter ID is taken from the signed-in account rather than trusted from a submitted form.

The status rules are kept in `TicketWorkflow`. Resolving records a solved date. Closing records a closed date and preserves any existing solved date. Reopening clears the completion dates. Separate dates are useful because a duplicate can be closed without the problem being solved.

The Open and Archived buttons show different sets of records from the same collection. The archive view keeps completed tickets accessible; it does not move them to another database.

### Individual functionality: transfer a ticket

The allowed-list feature assigned here is **transfer a ticket to another person**. Its separate class is `TicketTransferService`, in `NoSQL_Project/Services/TicketTransferService.cs`.

A service desk employee opens a ticket, chooses another service desk employee in the transfer form and submits it. The controller checks the role and passes the ticket and recipient IDs to the service. The service then checks that the ticket exists, the recipient is a service desk employee, and the ticket is not already assigned to that person. If those checks pass, it updates the assignee.

Suppose an incident is assigned to one colleague but another colleague should handle it. Transfer changes responsibility on that same ticket. Its ID, subject, description, status and dates remain the same. Creating a second ticket would split the incident across two records and affect the dashboard totals, so reassignment was preferred.

**A useful demonstration:** transfer a ticket and reopen its details to show the new assignee. Also check that a regular employee cannot call the transfer action. The role restriction is on the server, not just the button.

**Other main files:** `Controllers/TicketsController.cs`, `Repositories/TicketRepository.cs`, `Models/Ticket.cs`, `Services/TicketWorkflow.cs` and the views in `Views/Tickets/`, under `NoSQL_Project`.

## Maksym - Interface, setup and filtering

Maksym's part is the shared interface and setup. The pages use the same navigation, forms and table styles. Labels, validation messages and Cancel buttons make the forms easier to use. Status is written as text as well as shown with a colour. The server still checks access even when a link is hidden.

The importer supports a local demonstration using the supplied Employee and Ticket files. It preserves ObjectIds and refuses nonempty collections. For team use, both laptops need the same Atlas database. Private connection settings are not included in Git, and another person's changes appear after refreshing the page.

### Individual functionality: filter an already loaded list

This is the **filtering a preloaded ticket list using keywords** option from the allowed list. It uses a separate JavaScript class, `TicketFilter`, in `NoSQL_Project/wwwroot/js/ticket-filter.js`.

It works in three steps:

1. When the page opens, the class keeps references to the ticket rows already displayed.
2. As someone types, it compares the words with each row's text and hides rows that do not match.
3. Clearing the input brings the rows back. A counter shows how many loaded rows are visible.

Typing `High`, for example, keeps rows containing that word. Typing two words requires both to appear somewhere in the row. Matching ignores capital letters.

This belongs in the browser because the rows are already there. The class does not send a new request, change MongoDB records or fetch more tickets. It checks the current page, normally up to 20 rows. It cannot find a ticket on another page.

That is the difference from Jerrel's search. Search asks MongoDB for matching tickets; this control only narrows the loaded list. The page labels it **Filter this loaded page** to make that limit clear.

**To show it working:** type a keyword, watch rows disappear, then clear it and watch them return without reloading. Try a word that matches nothing as well.

**Other main files:** `Views/Shared/_Layout.cshtml`, `Views/Tickets/Index.cshtml`, `wwwroot/css/site.css`, `Services/DatabaseImporter.cs` and the setup instructions.

## Jerrel - Authentication, dashboard and search

Jerrel's area connects the signed-in employee to the rest of the application. Login verifies a stored password hash with ASP.NET's `PasswordHasher` and creates an authentication cookie containing the employee ID and role. Restricted actions are checked on the server. Regular employees see their own reported tickets; service desk staff can manage tickets across employees.

Both roles use the same dashboard view. MongoDB first limits the regular-employee query to the reporter ID, then groups the tickets by outcome. The view model calculates percentages and handles an empty result. A solved-then-closed ticket remains in the resolved outcome category, using the separate solved and closed dates from Delivery 1.

### Individual functionality: AND/OR ticket search

**Allowed option:** searching ticket subjects and content with AND/OR and newest results first.

**Separate class:** `TicketSearchService` in `NoSQL_Project/Services/TicketSearchService.cs`.

`BuildFilter` splits the query into words. Each word may appear in either the subject or the description. The selected mode determines how those word conditions are combined:

- **AND:** every word must match somewhere across the two fields.
- **OR:** at least one word must match.

For `printer office`, AND accepts a ticket with printer in its subject and office in its description. OR also accepts a ticket containing just one of those words. Matching ignores case, and regex characters are escaped so input such as `.*` stays literal text.

The controller combines the full search filter with the reporter restriction and the selected Open/Archived view. Choosing OR therefore does not expose another employee's tickets. `TicketRepository.Find` orders results by reported date descending, then by ID. Newest first is the default; the priority options let the user explicitly choose a different order.

The search runs in MongoDB, rather than checking only the current table page. Literal regex matching keeps the implementation small, but it is not a full-text indexed search system. A larger collection would need a suitable search index and measured query performance.

**Demonstration:** compare AND and OR with the same two words, check the date order under Newest first, and verify that a regular employee still cannot find another employee's tickets.

**Other main files:** `Controllers/AccountController.cs`, `Controllers/HomeController.cs`, `Program.cs`, the account/dashboard views and `Repositories/TicketRepository.cs`.

## Shared database choices

Employee and Ticket remain separate collections, as designed in Delivery 1. Small values such as name and contact details are embedded because they belong to one employee and are normally read together. Tickets reference employees using ObjectIds, avoiding repeated employee details in every incident.

MongoDB does not enforce these references as SQL foreign keys, so the application checks them before saving or deleting related records. Database queries stay in repositories, and the MongoClient is reused across requests.

## Testing and limitations

Development checks covered login, role restrictions, CRUD, dashboard calculations, search, sorting, transfer, filtering, and closing and reopening tickets. Integration brings these features together in the ticket list while keeping the ownership rules in place.

There are limits to the current implementation. Priority sorting and pagination run in application memory, regex search is not a full-text search system, and simultaneous edits can overwrite each other. Tickets also need a page refresh to show changes from another laptop. Database-side pagination, a suitable search index and edit-conflict detection would be useful improvements for a larger system.

## Individual feature check

All four features were found in the current clone, including their interface controls and controller/script connections. No additional application feature was needed for this check. Feature ownership in this document follows the group work allocation; code inspection alone does not establish who originally wrote a class.

The current solution built with zero warnings and errors. The existing 16 C# checks and six additional JavaScript filtering checks passed during this review. Transfer was checked in its service, protected action and form; this review did not transfer any live Atlas ticket. These are project verification results, not claims about which student ran the checks.
