using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Common;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

[TestClass]
internal sealed class WorkflowTests : IDisposable
{
    private readonly TestApp app = new();

    public WorkflowTests() => app.SeedDefaultUsers();

    public void Dispose() => app.Dispose();

    [TestMethod]
    public async Task ApproveMovesSubmittedToApprovedAndRecordsHistory()
    {
        Guid id = await app.ApprovedAsync("alice");

        ExpenseHistory last = (await app.HistoryAsync(id)).Last();
        Assert.AreEqual(ExpenseAction.Approved, last.Action);
        Assert.AreEqual("carol", last.ActorId);
        Assert.AreEqual(ExpenseStatus.Submitted, last.FromStatus);
        Assert.AreEqual(ExpenseStatus.Approved, last.ToStatus);
    }

    [TestMethod]
    public async Task ApproverCannotApproveOwnExpense()
    {
        Guid id = await app.SubmittedAsync("gina");

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => app.Workflow.ApproveAsync(id));
    }

    [TestMethod]
    [DataRow("alice")]
    [DataRow("dave")]
    [DataRow("erin")]
    [DataRow("frank")]
    public async Task OnlyApproverCanApprove(string user)
    {
        Guid id = await app.SubmittedAsync("bob");
        app.As(user);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => app.Workflow.ApproveAsync(id));
    }

    [TestMethod]
    public async Task ApproveTwiceIsConflictWithoutDuplicateHistory()
    {
        Guid id = await app.ApprovedAsync("alice");
        app.As("carol");

        await Assert.ThrowsExactlyAsync<ConflictException>(() => app.Workflow.ApproveAsync(id));
        Assert.HasCount(3, await app.HistoryAsync(id));
    }

    [TestMethod]
    public async Task ApproverCannotApproveSomeoneElsesDraft()
    {
        Guid id = await app.DraftAsync("alice");
        app.As("carol");

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => app.Workflow.ApproveAsync(id));
    }

    [TestMethod]
    public async Task RejectStoresJustificationAndHistory()
    {
        Guid id = await app.SubmittedAsync("alice");
        app.As("carol");

        ExpenseDecisionResponse rejected = await app.Workflow.RejectAsync(
            id, new RejectExpenseRequest { Justification = "  Falta comprovante fiscal  " });

        Assert.AreEqual(ExpenseStatus.Rejected, rejected.Status);
        Assert.AreEqual("Falta comprovante fiscal", rejected.RejectionReason);
        ExpenseHistory last = (await app.HistoryAsync(id)).Last();
        Assert.AreEqual(ExpenseAction.Rejected, last.Action);
        Assert.AreEqual("Falta comprovante fiscal", last.Justification);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("curta")]
    [DataRow("          ")]
    public async Task RejectRequiresValidJustification(string justification)
    {
        Guid id = await app.SubmittedAsync("alice");
        app.As("carol");

        await Assert.ThrowsExactlyAsync<BadRequestException>(
            () => app.Workflow.RejectAsync(id, new RejectExpenseRequest { Justification = justification }));
        Assert.HasCount(2, await app.HistoryAsync(id));
    }

    [TestMethod]
    public async Task RejectAfterApprovalIsConflict()
    {
        Guid id = await app.ApprovedAsync("alice");
        app.As("carol");

        await Assert.ThrowsExactlyAsync<ConflictException>(
            () => app.Workflow.RejectAsync(id, new RejectExpenseRequest { Justification = "Justificativa válida" }));
    }

    [TestMethod]
    public async Task ApproverCannotRejectOwnExpense()
    {
        Guid id = await app.SubmittedAsync("gina");

        await Assert.ThrowsExactlyAsync<ForbiddenException>(
            () => app.Workflow.RejectAsync(id, new RejectExpenseRequest { Justification = "Justificativa válida" }));
    }

    [TestMethod]
    public async Task PayCreatesPaymentRecordWithServerActorAndTime()
    {
        Guid id = await app.PaidAsync("alice");

        PaymentRecord payment = await app.Db.PaymentRecords.SingleAsync();
        Assert.AreEqual(id, payment.ExpenseId);
        Assert.AreEqual("dave", payment.PaidByUserId);
        Assert.AreEqual(app.Clock.GetUtcNow().UtcDateTime, payment.PaidAtUtc);
        Assert.AreEqual(ExpenseAction.Paid, (await app.HistoryAsync(id)).Last().Action);
    }

    [TestMethod]
    public async Task FinanceCannotPayOwnExpense()
    {
        Guid id = await app.ApprovedAsync("ivan");
        app.As("ivan");

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => app.Workflow.PayAsync(id));
    }

    [TestMethod]
    public async Task PayRequiresApprovedState()
    {
        Guid id = await app.SubmittedAsync("alice");
        app.As("dave");

        await Assert.ThrowsExactlyAsync<ConflictException>(() => app.Workflow.PayAsync(id));
    }

    [TestMethod]
    public async Task PayTwiceIsConflictWithSinglePayment()
    {
        Guid id = await app.PaidAsync("alice");
        app.As("dave");

        await Assert.ThrowsExactlyAsync<ConflictException>(() => app.Workflow.PayAsync(id));
        Assert.AreEqual(1, await app.Db.PaymentRecords.CountAsync());
    }

    [TestMethod]
    [DataRow("alice")]
    [DataRow("carol")]
    [DataRow("erin")]
    [DataRow("frank")]
    public async Task OnlyFinanceCanPay(string user)
    {
        Guid id = await app.ApprovedAsync("bob");
        app.As(user);

        await Assert.ThrowsExactlyAsync<ForbiddenException>(() => app.Workflow.PayAsync(id));
    }

    [TestMethod]
    public async Task HistoryListsEntriesInOrderForOwnerAndAuditor()
    {
        Guid id = await app.PaidAsync("alice");

        app.As("alice");
        List<ExpenseAction> actions = (await app.Workflow.GetHistoryAsync(id)).Select(entry => entry.Action).ToList();
        List<ExpenseAction> expected = [ExpenseAction.Created, ExpenseAction.Submitted, ExpenseAction.Approved, ExpenseAction.Paid];
        CollectionAssert.AreEqual(expected, actions);

        app.As("erin");
        Assert.HasCount(4, await app.Workflow.GetHistoryAsync(id));
    }

    [TestMethod]
    public async Task HistoryOfAnotherEmployeesExpenseIsNotFound()
    {
        Guid id = await app.DraftAsync("alice");
        app.As("bob");

        await Assert.ThrowsExactlyAsync<NotFoundException>(() => app.Workflow.GetHistoryAsync(id));
    }
}
