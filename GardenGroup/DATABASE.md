# MongoDB structure

The application uses two collections: `Employee` and `Ticket`. C# properties are mapped to camelCase by one convention registered before any database access. The MongoDB driver maps `Id` to `_id`. This implements Delivery 1 and now maps the exact names in the supplied Employee and Ticket JSON exports using explicit BsonElement attributes where needed.

The supplied dataset has 120 employees and 150 tickets. The original exports did not contain employee location or ticket type, priority and deadline fields. The distributed ticket JSON and local database copy now have suggested type/priority values based on title: 36 Network, 40 Hardware and 74 Software; 8 Critical, 76 High, 50 Normal and 16 Low. The original exports had 138 descriptions that did not match their titles. These were replaced with matching sample descriptions in the local/distributed copy; 12 already matching descriptions were retained. Original source files and Atlas were not changed. Titles were used as the guide for classification. These are editable demonstration classifications, not confirmed business-impact assessments. Deadline remains Not set until edited. The examples below show the complete supported structure.

## Employee example

```javascript
{
  _id: ObjectId("..."),
  name: { firstName: "Amanda", lastName: "Lewis" },
  contact: { email: "amanda.lewis@gardengroup.com", phone: "0200000000" },
  location: {
    name: "Haarlem", city: "Haarlem", province: "Noord-Holland",
    street: "Demo Street 1", postcode: "1000AA"
  },
  passwordHash: "<ASP.NET PasswordHasher output>",
  role: "Regular"
}
```

`role` is `Regular` or `ServiceDesk`. Email is trimmed and stored lowercase. The unique email index also uses case-insensitive collation. Passwords are never stored as plaintext. Each demo employee has an independently salted hash, even though the demo password is shared.

## Ticket example

```javascript
{
  _id: ObjectId("..."),
  reportedByEmployeeId: ObjectId("..."),
  assignedToEmployeeId: null,
  title: "Printer does not respond",
  type: "Hardware",
  description: "The branch printer does not respond when printing.",
  priority: "Normal",
  deadline: ISODate("2026-10-27T00:00:00Z"),
  statusInfo: {
    status: "Open",
    dateReported: ISODate("2026-10-20T00:00:00Z"),
    solvedAt: null,
    closedAt: null
  }
}
```

The user-entered date of the incident is stored in `statusInfo.dateReported`, retaining the original model's field name. This version does not store a separate creation audit timestamp. Dates are displayed and stored as UTC; date-only inputs become midnight UTC. For existing tickets, editing without changing the date preserves its time.

`assignedToEmployeeId` is nullable until a service desk employee takes the ticket. MongoDB does not create foreign-key constraints from these ObjectIds. The application validates reporter/assignee existence, accepts only service desk assignees, and blocks deleting referenced employees.

## Status rules

- Open: no solved/closed dates.
- Resolved: solved date exists; closed date is empty.
- Closed without a previous solution: closed date exists, solved date is empty.
- Closed after a solution: both dates exist.
- Reopening clears both completion dates, since there is no ticket-history collection.

New tickets always start Open, including tickets created by the service desk. Service desk staff can then edit the status. Regular employees cannot edit a ticket's status or transfer it.

## Dashboard aggregation

`TicketRepository.GetStatusCounts` runs an aggregation in MongoDB. For a regular employee it first applies `$match` on `reportedByEmployeeId`. For a service desk employee it includes all tickets. The `$group` key places each ticket into exactly one category:

1. Open if its status is Open.
2. Resolved if its status is Resolved, or it was closed with a solved date.
3. Closed without resolution otherwise.

The view model calculates `count / total * 100`, rounds to one decimal, and returns zero when total is zero. The categories cover all tickets when data follows the documented status rules. The supplied 150 tickets give 74 open (49.3%), 49 resolved (32.7%) and 27 closed without resolution (18%).

## Search and other queries

- Search splits input into up to 10 words. Each word matches subject OR description. These word clauses are then combined with AND or OR. Regex metacharacters are escaped, so user input stays literal text.
- The ownership filter is ANDed with the complete search filter. Choosing OR cannot escape the regular employee's scope.
- Search results use `statusInfo.dateReported` descending and `_id` descending, so ties are deterministic.
- Priority sorting ranks Critical, High, Normal and Low explicitly; alphabetical sorting would give the wrong order. The current implementation sorts scoped matches in application memory before pagination.
- Browser filtering only hides/shows the 20 already rendered rows. It sends no database request and is separate from searching all matching database tickets.
- Transfer validates the recipient and replaces the ticket under its existing `_id`, preserving the other values.

## Indexes

- Unique case-insensitive `Employee.contact.email` index.
- Compound `Ticket.reportedByEmployeeId` ascending + `statusInfo.dateReported` descending, for an employee's recent-ticket query.
- `Ticket.assignedToEmployeeId`, for reference checks.
- `Ticket.statusInfo.dateReported` descending, for the service desk list.

These indexes do not accelerate arbitrary unanchored regex word searches; search scans the candidate tickets. This is acceptable for the classroom dataset. A larger system would need measured query plans, database pagination, and a suitable text/search index rather than claiming these regex queries are indexed.

## Limitations

The assignment uses a small database and a standalone MongoDB instance. CRUD operations on one document are atomic, but this version does not use multi-document transactions or optimistic concurrency. Two simultaneous edits can overwrite each other, and concurrent employee deletion/reference creation has a race between validation and the write. These should be addressed before using the application as a real service desk. Branch details are repeated inside employee documents, so a branch address change needs updates to multiple employees.
