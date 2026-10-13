using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Admin;
using ExpenseHub.Api.Common;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class RequestValidationTests
{
    private static bool IsValid(object request)
        => Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true);

    private static ExpenseRequest Request(string description = "Almoço com cliente", decimal amount = 10m, DateOnly? date = null)
        => new()
        {
            Description = description,
            Amount = amount,
            ExpenseDate = date ?? new DateOnly(2024, 1, 15),
        };

    [TestMethod]
    public void ValidRequestPasses() => Assert.IsTrue(IsValid(Request()));

    [TestMethod]
    [DataRow("curta")]
    [DataRow("")]
    public void ShortDescriptionIsInvalid(string description) => Assert.IsFalse(IsValid(Request(description)));

    [TestMethod]
    public void LongDescriptionIsInvalid() => Assert.IsFalse(IsValid(Request(new string('a', 501))));

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(2147483648.0)]
    public void AmountOutsideRangeIsInvalid(double amount) => Assert.IsFalse(IsValid(Request(amount: (decimal)amount)));

    [TestMethod]
    [DataRow(0.01)]
    [DataRow(2147483647.0)]
    public void AmountAtLimitsIsValid(double amount) => Assert.IsTrue(IsValid(Request(amount: (decimal)amount)));

    [TestMethod]
    public void FutureDateIsInvalid()
        => Assert.IsFalse(IsValid(Request(date: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))));

    [TestMethod]
    public void TodayIsValid()
        => Assert.IsTrue(IsValid(Request(date: DateOnly.FromDateTime(DateTime.UtcNow))));

    [TestMethod]
    public void DefaultDateIsInvalid() => Assert.IsFalse(IsValid(Request(date: DateOnly.MinValue)));

    [TestMethod]
    public void RejectJustificationMustHaveAtLeastTenCharacters()
    {
        Assert.IsFalse(IsValid(new RejectExpenseRequest { Justification = "curta" }));
        Assert.IsTrue(IsValid(new RejectExpenseRequest { Justification = "Justificativa válida" }));
    }
}

[TestClass]
internal sealed class AdminTests : IDisposable
{
    private readonly TestApp app = new();

    public AdminTests()
    {
        app.AddUser("frank", Roles.Admin);
        app.AddUser("alice", Roles.Employee);
    }

    public void Dispose() => app.Dispose();

    [TestMethod]
    public async Task AdminListsUsersWithRoles()
    {
        app.As("frank");

        IReadOnlyList<UserResponse> users = await app.Admin.ListUsersAsync();

        Assert.HasCount(2, users);
        Assert.IsTrue(users.Single(user => user.Id == "alice").Roles.Contains(Roles.Employee));
    }

    [TestMethod]
    public async Task NonAdminCannotListUsers()
    {
        app.As("alice");

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => app.Admin.ListUsersAsync());
    }

    [TestMethod]
    public async Task UpdateRolesRejectsUnknownRole()
    {
        app.As("frank");

        await Assert.ThrowsExactlyAsync<BadRequestException>(
            () => app.Admin.UpdateRolesAsync("alice", new UpdateRolesRequest { Roles = ["SuperUser"] }));
    }

    [TestMethod]
    public async Task AdminCannotRemoveOwnAdminRole()
    {
        app.As("frank");

        await Assert.ThrowsExactlyAsync<BadRequestException>(
            () => app.Admin.UpdateRolesAsync("frank", new UpdateRolesRequest { Roles = [Roles.Employee] }));
    }

    [TestMethod]
    public async Task UpdateRolesAddsAndRemovesRoles()
    {
        app.As("frank");

        UserResponse updated = await app.Admin.UpdateRolesAsync(
            "alice", new UpdateRolesRequest { Roles = [Roles.Approver, Roles.Finance] });

        CollectionAssert.AreEqual(new List<string> { Roles.Approver, Roles.Finance }, updated.Roles.ToList());
        UserResponse listed = (await app.Admin.ListUsersAsync()).Single(user => user.Id == "alice");
        Assert.IsFalse(listed.Roles.Contains(Roles.Employee));
    }

    [TestMethod]
    public async Task UpdateRolesOfUnknownUserIsNotFound()
    {
        app.As("frank");

        await Assert.ThrowsExactlyAsync<NotFoundException>(
            () => app.Admin.UpdateRolesAsync("ghost", new UpdateRolesRequest { Roles = [Roles.Employee] }));
    }
}
