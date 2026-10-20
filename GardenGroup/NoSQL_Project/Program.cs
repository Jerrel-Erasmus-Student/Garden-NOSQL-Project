using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using NoSQL_Project.Models;
using NoSQL_Project.Repositories;
using NoSQL_Project.Repositories.Interfaces;
using NoSQL_Project.Services;

ConventionRegistry.Register("GardenCamelCase", new ConventionPack { new CamelCaseElementNameConvention() },
    type => type.Namespace?.StartsWith("NoSQL_Project") == true);
var seed = args.Contains("--seed-demo");
var indexes = args.Contains("--ensure-indexes");
var import = args.Contains("--import-data");
var resetPasswords = args.Contains("--reset-imported-passwords");
var builder = WebApplication.CreateBuilder(args.Where(a => !new[] { "--seed-demo", "--ensure-indexes", "--import-data", "--reset-imported-passwords" }.Contains(a)).ToArray());
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddSingleton<IMongoClient>(_ => {
    var settings = MongoClientSettings.FromConnectionString(builder.Configuration["Mongo:ConnectionString"] ?? "mongodb://127.0.0.1:27017");
    settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
    return new MongoClient(settings);
});
builder.Services.AddScoped(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(builder.Configuration["Mongo:Database"] ?? "GardenGroupDelivery3"));
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<IPasswordHasher<Employee>, PasswordHasher<Employee>>();
builder.Services.AddScoped<TicketSearchService>();
builder.Services.AddScoped<PrioritySortService>();
builder.Services.AddScoped<TicketTransferService>();
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddScoped<DatabaseImporter>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => {
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Denied";
    options.Cookie.Name = "GardenGroup.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
    options.Events.OnValidatePrincipal = async context => {
        var rawId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var employee = ObjectId.TryParse(rawId, out var id)
            ? await context.HttpContext.RequestServices.GetRequiredService<IEmployeeRepository>().GetById(id) : null;
        if (employee == null || employee.Role != context.Principal?.FindFirstValue(ClaimTypes.Role) ||
            employee.PasswordHash != context.Principal?.FindFirstValue("passwordVersion")) {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    };
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllersWithViews(options => {
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
var app = builder.Build();
if (import) {
    var directory = builder.Configuration["Import:Directory"] ?? throw new InvalidOperationException("Set Import__Directory to the folder containing Garden.Employee.json and Garden.Ticket.json.");
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseImporter>().Import(directory, resetPasswords);
    return;
}
if (seed) {
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoSeeder>().Seed();
    return;
}
if (indexes) {
    using var scope = app.Services.CreateScope();
    await DemoSeeder.EnsureIndexes(scope.ServiceProvider.GetRequiredService<IMongoDatabase>());
    Console.WriteLine("MongoDB indexes are ready.");
    return;
}
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; frame-ancestors 'none'; form-action 'self'; base-uri 'self'";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePages("text/plain", "Request could not be completed (HTTP {0}).");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
