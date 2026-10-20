# NoSQL questions to practise

Use these notes to understand the implementation. Practise explaining the ideas without reading a prepared answer word for word. If you have not worked on a part, say that honestly and explain what you learned from reviewing it.

Your Delivery 1 already contains the relational and MongoDB models and the design explanation. You do not need to invent another model. Start by explaining how this application implements those existing choices. DATABASE.md only records the exact field names used by the code.

## Why MongoDB?

The project requires a NoSQL database. MongoDB stores BSON documents, which fit an employee with nested name/contact/location data and a ticket with nested dates. C# classes map naturally to these documents. That does not mean MongoDB is automatically better than SQL for every service desk.

## What is BSON?

It is MongoDB's binary document format. It supports values such as ObjectId and dates as well as strings and nested objects. The examples in DATABASE.md are readable representations; an ObjectId is not stored as a SQL integer or just ordinary display text.

## Why two collections?

An employee is an account. A ticket is an incident with its own lifecycle. The service desk often needs all tickets regardless of who reported them. Keeping tickets in their own collection makes that query straightforward and avoids an ever-growing ticket list inside each employee.

## Why embed contact details but reference the reporter?

Contact details are small and belong to one employee. They are normally used together, so they are embedded. An employee can appear on many tickets. Referencing their ID avoids repeating the whole employee document each time. Changing the employee's contact details then happens in one place.

## Could you embed the employee in each ticket?

Yes. That could make a ticket readable without another lookup, or preserve the name at the time of reporting. It would duplicate data and need a clear rule about whether old tickets show old or current employee details. This project uses references and displays current names.

## Why isn't location a separate collection?

Embedding keeps the design small, but repeats branch addresses. If the app needed branch management, a Branch collection and employee branch references would be a better choice. This is a tradeoff, not a rule that location should always be embedded.

## Does MongoDB enforce the employee references?

No. An ObjectId reference is just a stored ID. Our controller/service checks that the employee exists. It also blocks deleting an employee referenced by tickets. In SQL a foreign key could enforce this relationship in the database. This app's cross-document checks are not transactional, which matters if two users change related data at the same time.

## What does an aggregation pipeline do here?

It processes documents in stages. For a regular employee the pipeline first matches their reported tickets. It then groups the tickets by outcome and counts them. MongoDB returns a few counts rather than the full ticket collection. The C# view model calculates the percentages from those counts.

Example: 10 tickets, with 4 open, 5 resolved and 1 closed without a solution, gives 40%, 50% and 10%. No tickets gives 0% in each category. Your supplied dataset has 150 tickets: 74 open, 49 resolved and 27 closed without resolution, giving 49.3%, 32.7% and 18%.

## Why not count the tickets in the browser?

The browser only sees a page of tickets. Counting that page would give incorrect totals. Aggregation counts the full authorised set in MongoDB. Fetching every ticket just to count statuses also transfers more data than needed.

## What is the difference between resolved and closed?

Resolved means a solution was recorded. Closed means the ticket has been finished administratively. A duplicate can be closed without a solution. SolvedAt and ClosedAt let the dashboard tell the difference. Closing a resolved ticket keeps SolvedAt, so it still contributes to the resolved category.

## Which indexes did you choose?

Email has a unique case-insensitive index to prevent duplicate accounts. Tickets have indexes for reporter/date, assignee and date. The reporter/date compound index suits the query for one employee's recent tickets. Indexes speed up relevant reads but use storage and add work when writing documents. They should follow actual query patterns.

## Is your word search indexed?

Not as a full-text search. It uses escaped, case-insensitive regular expressions on subject and description. The ownership scope can narrow the candidate set, but ordinary indexes do not make arbitrary substring regex searches fast. For this small dataset that is acceptable. A larger version should use an appropriate search/text index and measure its queries.

## How does AND/OR work?

Searching `printer office` with AND means both words must occur somewhere across subject or description. OR means at least one of them must occur. In both cases the regular employee's reporter restriction is still applied outside the complete search condition. A special character like `*` is escaped instead of becoming a regular-expression command.

## How is filtering different from searching?

Search sends a request and queries the database. The loaded-page filter only checks rows that are already in the browser. For example, typing `Critical` in the loaded filter hides other priorities on that page; it does not find Critical tickets on other pages. Clearing the field shows those loaded rows again.

## Why does priority need its own sort rule?

Alphabetically, Critical/High/Low/Normal is not a useful urgency scale. The service assigns numbers: Critical 4, High 3, Normal 2, Low 1. Equal priorities are ordered by newest date. In this version sorting happens in C# before pagination; with much more data it should move into a database query/pipeline.

## How are queries grouped?

The ticket-list action loads its scoped matches once. For service desk staff, employee names are loaded together and placed in a dictionary, rather than running one employee query for every displayed row. Dashboard counts are grouped in MongoDB. The details page only needs the reporter and optional assignee lookups for that one ticket.

## Why use a repository?

It separates MongoDB queries from request handling and HTML. A controller checks the user's role and the form, then asks the repository to store or retrieve a document. This also makes it easier to locate and explain the database operations.

## Is a NoSQL database schema-free?

It allows flexible documents, but the application still needs a consistent structure. This version expects defined roles, statuses, nested objects, ObjectIds and date fields. C# models and validation describe those expectations. We do not claim arbitrary old documents will work without migration.

## Why not store plaintext passwords?

Anyone who obtained the database would immediately obtain every password. A salted password hash supports verification without storing the original password. The built-in PasswordHasher implements this. The publicly documented demo password is only for the fictional demo database.

## What would you improve with more time?

Add database-side pagination/sorting for a larger dataset, a measured search index, a branch collection if branch management is needed, ticket history, and protection against simultaneous edits. Use transactions where cross-document consistency needs a stronger guarantee. Do not say these are implemented in the current version.

## Before the demonstration

1. Log in as a regular employee and explain the ownership filter.
2. Create a ticket and point out its new Open state.
3. Log in as service desk staff and explain why the list/dashboard changes.
4. Resolve and close a ticket; explain the two dates and resulting dashboard category.
5. Show your individual feature and its separate class.
6. Open DATABASE.md and identify one embedded object and one reference.
7. Explain one design tradeoff you understand, rather than claiming your design has no disadvantages.
