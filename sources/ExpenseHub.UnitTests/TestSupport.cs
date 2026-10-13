using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Admin;
using ExpenseHub.Api.Common;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DiscoverInternals]

namespace ExpenseHub.UnitTests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
internal sealed class TestHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
internal sealed class TestApp : IDisposable
{
    private readonly TestHttpContextAccessor accessor = new() { HttpContext = new DefaultHttpContext() };
    private readonly Dictionary<string, string[]> rolesByUser = [];
    private readonly ServiceProvider provider;
    private readonly IServiceScope scope;

    public TestApp()
    {
        string databaseName = Guid.NewGuid().ToString();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IHttpContextAccessor>(accessor);
        services.AddScoped<CurrentUser>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<ExpenseWorkflowService>();
        services.AddScoped<AdminService>();

        provider = services.BuildServiceProvider();
        scope = provider.CreateScope();
        Db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Db.Database.EnsureCreated();

        foreach (string role in Roles.All)
        {
            Db.Roles.Add(new IdentityRole(role) { NormalizedName = role.ToUpperInvariant() });
        }

        Db.SaveChanges();
    }

    public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));

    public AppDbContext Db { get; }

    public ExpenseService Expenses => scope.ServiceProvider.GetRequiredService<ExpenseService>();

    public ExpenseWorkflowService Workflow => scope.ServiceProvider.GetRequiredService<ExpenseWorkflowService>();

    public AdminService Admin => scope.ServiceProvider.GetRequiredService<AdminService>();

    public static ExpenseRequest ValidRequest() => new()
    {
        Description = "Almoço com cliente",
        Amount = 150.50m,
        ExpenseDate = new DateOnly(2024, 1, 15),
    };

    public void SeedDefaultUsers()
    {
        AddUser("alice", Roles.Employee);
        AddUser("bob", Roles.Employee);
        AddUser("carol", Roles.Approver);
        AddUser("dave", Roles.Finance);
        AddUser("erin", Roles.Auditor);
        AddUser("frank", Roles.Admin);
        AddUser("gina", Roles.Employee, Roles.Approver);
        AddUser("ivan", Roles.Employee, Roles.Finance);
        AddUser("hank");
    }

    public void AddUser(string name, params string[] roles)
    {
        string email = $"{name}@test.local";
        Db.Users.Add(new ApplicationUser
        {
            Id = name,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            SecurityStamp = Guid.NewGuid().ToString(),
        });

        foreach (string role in roles)
        {
            string roleId = Db.Roles.Single(r => r.Name == role).Id;
            Db.UserRoles.Add(new IdentityUserRole<string> { UserId = name, RoleId = roleId });
        }

        Db.SaveChanges();
        rolesByUser[name] = roles;
    }

    public void As(string name)
    {
        List<Claim> claims = [new Claim(ClaimNames.Subject, name)];
        claims.AddRange(rolesByUser[name].Select(role => new Claim(ClaimNames.Role, role)));
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
    }

    public async Task<Guid> DraftAsync(string owner)
    {
        As(owner);
        ExpenseResponse created = await Expenses.CreateAsync(ValidRequest());
        return created.Id;
    }

    public async Task<Guid> SubmittedAsync(string owner)
    {
        Guid id = await DraftAsync(owner);
        await Expenses.SubmitAsync(id);
        return id;
    }

    public async Task<Guid> ApprovedAsync(string owner)
    {
        Guid id = await SubmittedAsync(owner);
        As("carol");
        await Workflow.ApproveAsync(id);
        return id;
    }

    public async Task<Guid> PaidAsync(string owner)
    {
        Guid id = await ApprovedAsync(owner);
        As("dave");
        await Workflow.PayAsync(id);
        return id;
    }

    public Task<List<ExpenseHistory>> HistoryAsync(Guid expenseId)
        => Db.ExpenseHistories.Where(history => history.ExpenseId == expenseId).OrderBy(history => history.Id).ToListAsync();

    public void Dispose()
    {
        scope.Dispose();
        provider.Dispose();
    }
}
