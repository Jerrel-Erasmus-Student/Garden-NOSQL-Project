# Garden Group - Delivery 3
## Application Components and Design Choices

**Group members:** Ahmed, Denys, Maksym and Jerrel  
**GitHub:** https://github.com/Jerrel-Erasmus-Student/Garden-NOSQL-Project

## Application overview

The application lets Garden Group employees report problems and follow their tickets. Service desk employees can manage all tickets and employee accounts. Both roles have a dashboard showing open, resolved and closed-without-resolution tickets.

The project uses C#, ASP.NET Core MVC and MongoDB. It builds on the database model from Delivery 1 and the basic CRUD methods from the starting project. Controllers handle requests and validation, repositories contain database queries, and Razor views display the results.

## Work allocation

| Member | Main components | Additional feature |
| --- | --- | --- |
| Ahmed | Employee management and account validation | Priority sorting |
| Denys | Ticket management and status changes | Ticket transfer |
| Maksym | Interface, navigation and application setup | Filtering loaded tickets |
| Jerrel | Login, permissions and dashboard | AND/OR ticket search |

## Ahmed - Employee management and sorting

Ahmed's allocated area is employee management. Service desk staff can create accounts, edit employee details and roles, and delete accounts when they are not referenced by tickets.

`EmployeesController` validates the form before calling `EmployeeRepository`. A separate `EmployeeForm` controls which values can be submitted. New accounts require a password, while leaving the password field blank during an edit keeps the existing hash. Duplicate emails are rejected.

Employee deletion is blocked when tickets reference the employee as reporter or assignee. Deleting their tickets automatically would remove incident records, so keeping those relationships intact was preferred. Service desk staff also cannot delete their own account or remove their own service desk access.

The additional feature is the separate `PrioritySortService` class. It ranks Critical above High, Normal and Low. Alphabetical sorting would give the wrong urgency order. Users can sort either way, and equal priorities are ordered by reported date. Sorting is applied before pagination so it covers the full result set.

**Main files:** `EmployeesController.cs`, `EmployeeRepository.cs`, the employee views, `EmployeeForm` and `PrioritySortService.cs`.

## Denys - Ticket management and transfer

Denys's allocated area is ticket handling: creating tickets, displaying details, editing incidents and changing their status. New tickets start Open. For regular employees, the reporter ID comes from their signed-in account rather than being trusted from the form.

`TicketRepository` handles storage, while `TicketWorkflow` keeps the status rules together. Resolving records a solved date. Closing records a closed date and keeps any existing solved date. Reopening clears the completion dates.

Separate solved and closed dates follow the Delivery 1 design. A duplicate ticket can be closed without its problem being solved, so one completion date would not describe both outcomes correctly.

The main ticket list shows Open incidents. The archive button shows Resolved and Closed incidents from the same collection. Records remain available, and the dashboard includes both views. Permanent deletion has a separate confirmation screen.

The additional feature is `TicketTransferService`. It checks that the recipient is a different, existing service desk employee, then changes the assignee while keeping the ticket's other details. Creating a duplicate ticket for the new assignee would make the incident harder to track and affect the totals.

**Main files:** `TicketsController.cs`, `TicketRepository.cs`, `Ticket.cs`, the ticket views, `TicketWorkflow.cs` and `TicketTransferService.cs`.

## Maksym - Interface, setup and filtering

Maksym's allocated area is the shared layout, navigation, ticket-list presentation and setup instructions. The interface uses consistent forms and tables so the application remains easy to follow. Labels, validation messages and Cancel buttons help users complete forms. Status is shown with text as well as colour.

The shared Razor layout and CSS avoid repeating the same design across pages. A large front-end framework was unnecessary for these screens. Navigation reflects each role, while server-side checks still protect restricted actions.

The additional feature is the separate JavaScript class `TicketFilter`. It filters rows already loaded on the current page. Typing hides nonmatching rows, and clearing the input restores them. This needs no database request because those rows are already in the browser. It cannot find tickets on other pages, which is why it is labelled separately from database search.

The setup includes importing the supplied Employee and Ticket exports while preserving their ObjectIds. Importing into nonempty collections is refused to avoid overwriting existing records. Shared use requires both laptops to connect to the same Atlas database. Private connection settings are excluded from Git.

**Main files:** `_Layout.cshtml`, `Views/Tickets/Index.cshtml`, `site.css`, `ticket-filter.js`, `DatabaseImporter.cs` and the setup instructions.

## Jerrel - Login, permissions, dashboard and search

Jerrel's allocated area is authentication, access control and the shared dashboard. Login checks the employee's email and verifies their password with ASP.NET's `PasswordHasher`. The database stores a salted hash rather than the original password. A successful login creates an authentication cookie containing the employee ID and role.

Permissions are enforced on the server. Regular employees can view their own reported tickets; service desk staff can manage tickets across employees. Hiding buttons alone would not prevent someone opening a restricted URL. Existing sessions are also checked against account changes, and form submissions use anti-forgery validation.

Both roles use the same dashboard view. MongoDB first restricts regular-employee queries to their reporter ID, then groups tickets into outcome categories. The view model calculates percentages and handles zero tickets. Counting only the visible table page would give incorrect totals.

The additional feature is `TicketSearchService`. AND requires every search word across the subject or description; OR accepts at least one. Search ignores letter case and escapes regex characters so the input stays literal. The search condition is combined with ownership and open/archive restrictions. Results default to newest first.

**Main files:** `AccountController.cs`, `HomeController.cs`, authentication setup in `Program.cs`, the account and dashboard views, and `TicketSearchService.cs`.

## Shared database choices

Employee and Ticket remain separate collections, as designed in Delivery 1. Small values such as name and contact details are embedded because they belong to one employee and are normally read together. Tickets reference employees using ObjectIds, avoiding repeated employee details in every incident.

MongoDB does not enforce these references as SQL foreign keys, so the application checks them before saving or deleting related records. Database queries stay in repositories, and the MongoClient is reused across requests.

## Testing and limitations

Development checks covered login, role restrictions, CRUD, dashboard calculations, search, sorting, transfer, filtering, and closing and reopening tickets. Integration brings these features together in the ticket list while keeping the ownership rules in place.

There are limits to the current implementation. Priority sorting and pagination run in application memory, regex search is not a full-text search system, and simultaneous edits can overwrite each other. Tickets also need a page refresh to show changes from another laptop. Database-side pagination, a suitable search index and edit-conflict detection would be useful improvements for a larger system.
