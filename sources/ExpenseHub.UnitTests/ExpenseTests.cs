using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Common;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class ExpenseTests : IDisposable
{
    private readonly TestApp app = new();

    public ExpenseTests() => app.SeedDefaultUsers();

    public void Dispose() => app.Dispose();

    [TestMethod]
    public async Task CreateUsesOwnerFromTokenAndStartsAsDraft()
    {
        Guid id = await app.DraftAsync("alice");

        app.As("alice");
        ExpenseResponse created = await app.Expenses.GetAsync(id);
        Assert.AreEqual("alice", created.OwnerId);
        Assert.AreEqual(ExpenseStatus.Draft, created.Status);
        List<ExpenseHistory> history = await app.HistoryAsync(id);
        Assert.HasCount(1, history);
        Assert.AreEqual(ExpenseAction.Created, history[0].Action);
        Assert.AreEqual("alice", history[0].ActorId);
        Assert.IsNull(history[0].FromStatus);
        Assert.AreEqual(ExpenseStatus.Draft, history[0].ToStatus);
    }

    [TestMethod]
    [DataRow("carol")]
    [DataRow("dave")]
    [DataRow("erin")]
    [DataRow("frank")]
    [DataRow("hank")]
    public async Task CreateRequiresEmployeeRole(string user)
    {
        app.As(user);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => app.Expenses.CreateAsync(TestApp.ValidRequest()));
    }

    [TestMethod]
    public async Task CreateRejectsUnknownCategory()
    {
        app.As("alice");
        ExpenseRequest request = new()
        {
            Description = "Almoço com cliente",
            Amount = 10m,
            ExpenseDate = new DateOnly(2024, 1, 15),
            CategoryId = 999,
        };

        await Assert.ThrowsExactlyAsync<BadRequestException>(() => app.Expenses.CreateAsync(request));
    }

    [TestMethod]
    public async Task UpdateByOwnerChangesFieldsAndRecordsHistory()
    {
        Guid id = await app.DraftAsync("alice");
        ExpenseRequest request = new()
        {
            Description = "Almoço com cliente X",
            Amount = 200m,
            ExpenseDate = new DateOnly(2024, 1, 15),
            CategoryId = 1,
        };

        ExpenseResponse updated = await app.Expenses.UpdateAsync(id, request);

        Assert.AreEqual(200m, updated.Amount);
        Assert.AreEqual(1, updated.CategoryId);
        List<ExpenseHistory> history = await app.HistoryAsync(id);
        Assert.HasCount(2, history);
        Assert.AreEqual(ExpenseAction.Updated, history[1].Action);
        Assert.IsTrue(history[1].Changes!.Contains("Valor", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task UpdateWithoutChangesDoesNotCreateHistory()
    {
        Guid id = await app.DraftAsync("alice");

        await app.Expenses.UpdateAsync(id, TestApp.ValidRequest());

        Assert.HasCount(1, await app.HistoryAsync(id));
    }

    [TestMethod]
    public async Task UpdateByAnotherEmployeeIsNotFound()
    {
        Guid id = await app.DraftAsync("alice");
        app.As("bob");

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => app.Expenses.UpdateAsync(id, TestApp.ValidRequest()));
    }

    [TestMethod]
    public async Task UpdateAfterSubmitIsConflict()
    {
        Guid id = await app.SubmittedAsync("alice");

        await Assert.ThrowsExactlyAsync<ConflictException>(() => app.Expenses.UpdateAsync(id, TestApp.ValidRequest()));
    }

    [TestMethod]
    public async Task SubmitMovesDraftToSubmitted()
    {
        Guid id = await app.SubmittedAsync("alice");

        app.As("alice");
        ExpenseResponse found = await app.Expenses.GetAsync(id);
        Assert.AreEqual(ExpenseStatus.Submitted, found.Status);
        Assert.HasCount(2, await app.HistoryAsync(id));
    }

    [TestMethod]
    public async Task SubmitTwiceIsConflictWithoutDuplicateHistory()
    {
        Guid id = await app.SubmittedAsync("alice");

        await Assert.ThrowsExactlyAsync<ConflictException>(() => app.Expenses.SubmitAsync(id));
        Assert.HasCount(2, await app.HistoryAsync(id));
    }

    [TestMethod]
    public async Task SubmitByAnotherEmployeeIsNotFound()
    {
        Guid id = await app.DraftAsync("alice");
        app.As("bob");

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => app.Expenses.SubmitAsync(id));
    }

    [TestMethod]
    public async Task EmployeeListsOnlyOwnExpenses()
    {
        Guid own = await app.DraftAsync("alice");
        await app.DraftAsync("bob");
        app.As("alice");

        IReadOnlyList<ExpenseResponse> list = await app.Expenses.ListAsync();

        Assert.HasCount(1, list);
        Assert.AreEqual(own, list[0].Id);
    }

    [TestMethod]
    public async Task ApproverListsOnlySubmitted()
    {
        await app.DraftAsync("alice");
        Guid submitted = await app.SubmittedAsync("alice");
        app.As("carol");

        IReadOnlyList<ExpenseResponse> list = await app.Expenses.ListAsync();

        Assert.HasCount(1, list);
        Assert.AreEqual(submitted, list[0].Id);
    }

    [TestMethod]
    public async Task FinanceListsOnlyApprovedAndPaid()
    {
        await app.SubmittedAsync("alice");
        Guid approved = await app.ApprovedAsync("alice");
        Guid paid = await app.PaidAsync("bob");
        app.As("dave");

        HashSet<Guid> ids = (await app.Expenses.ListAsync()).Select(expense => expense.Id).ToHashSet();

        Assert.HasCount(2, ids);
        Assert.Contains(approved, ids);
        Assert.Contains(paid, ids);
    }

    [TestMethod]
    public async Task AuditorListsEverything()
    {
        await app.DraftAsync("alice");
        await app.SubmittedAsync("bob");
        app.As("erin");

        Assert.HasCount(2, await app.Expenses.ListAsync());
    }

    [TestMethod]
    [DataRow("frank")]
    [DataRow("hank")]
    public async Task AdminOrUserWithoutRoleCannotList(string user)
    {
        app.As(user);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => app.Expenses.ListAsync());
    }

    [TestMethod]
    public async Task AccumulatedRolesGetUnionOfVisibility()
    {
        await app.DraftAsync("bob");
        await app.SubmittedAsync("bob");
        await app.DraftAsync("gina");
        app.As("gina");

        Assert.HasCount(2, await app.Expenses.ListAsync());
    }

    [TestMethod]
    public async Task EmployeeCannotReadAnotherEmployeesExpense()
    {
        Guid id = await app.DraftAsync("alice");
        app.As("bob");

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => app.Expenses.GetAsync(id));
    }

    [TestMethod]
    public async Task AuditorCanReadAnyExpense()
    {
        Guid id = await app.DraftAsync("alice");
        app.As("erin");

        ExpenseResponse found = await app.Expenses.GetAsync(id);

        Assert.AreEqual(id, found.Id);
    }
}
