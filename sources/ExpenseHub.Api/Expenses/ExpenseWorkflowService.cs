using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Common;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Expenses;

internal sealed class ExpenseWorkflowService(AppDbContext db, CurrentUser currentUser, TimeProvider clock)
{
    private const string NotFoundMessage = "Reembolso não encontrado.";
    private const int MinTextLength = 10;
    private const int MaxTextLength = 500;

    private static readonly string[] ReaderRoles = [Roles.Employee, Roles.Approver, Roles.Finance, Roles.Auditor];

    public async Task<ExpenseDecisionResponse> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        string userId = RequireUserId();
        RequireRole(Roles.Approver);

        Expense expense = await LoadForDecisionAsync(id, userId, ct);
        EnsureStatus(expense, ExpenseStatus.Submitted);
        return await ChangeStatusAsync(expense, ExpenseStatus.Approved, ExpenseAction.Approved, userId, Now(), null, ct);
    }

    public async Task<ExpenseDecisionResponse> RejectAsync(Guid id, RejectExpenseRequest request, CancellationToken ct = default)
    {
        string userId = RequireUserId();
        RequireRole(Roles.Approver);
        RequestValidator.Validate(request);
        string justification = NormalizeText(request.Justification, "A justificativa");

        Expense expense = await LoadForDecisionAsync(id, userId, ct);
        EnsureStatus(expense, ExpenseStatus.Submitted);
        expense.RejectionReason = justification;
        return await ChangeStatusAsync(expense, ExpenseStatus.Rejected, ExpenseAction.Rejected, userId, Now(), justification, ct);
    }

    public async Task<ExpenseDecisionResponse> PayAsync(Guid id, CancellationToken ct = default)
    {
        string userId = RequireUserId();
        RequireRole(Roles.Finance);

        Expense expense = await LoadForDecisionAsync(id, userId, ct);
        EnsureStatus(expense, ExpenseStatus.Approved);

        DateTime now = Now();
        db.PaymentRecords.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(),
            ExpenseId = expense.Id,
            PaidByUserId = userId,
            PaidAtUtc = now,
            Amount = expense.Amount,
        });

        return await ChangeStatusAsync(expense, ExpenseStatus.Paid, ExpenseAction.Paid, userId, now, null, ct);
    }

    public async Task<IReadOnlyList<HistoryEntryResponse>> GetHistoryAsync(Guid id, CancellationToken ct = default)
    {
        string userId = RequireUserId();
        RequireAnyRole(ReaderRoles);

        bool visible = await VisibleExpenses(db.Expenses.AsNoTracking(), userId).AnyAsync(expense => expense.Id == id, ct);
        if (!visible)
        {
            throw new NotFoundException(NotFoundMessage);
        }

        List<ExpenseHistory> entries = await db.ExpenseHistories.AsNoTracking()
            .Where(history => history.ExpenseId == id)
            .OrderBy(history => history.Id)
            .ToListAsync(ct);

        return entries
            .Select(entry => new HistoryEntryResponse(
                entry.Id,
                entry.Action,
                entry.ActorId,
                AsUtc(entry.OccurredAtUtc),
                entry.FromStatus,
                entry.ToStatus,
                entry.Justification,
                entry.Changes))
            .ToList();
    }

    private async Task<ExpenseDecisionResponse> ChangeStatusAsync(
        Expense expense, ExpenseStatus next, ExpenseAction action, string userId, DateTime now, string? justification, CancellationToken ct)
    {
        ExpenseStatus previous = expense.Status;
        expense.Status = next;
        expense.UpdatedAtUtc = now;

        db.ExpenseHistories.Add(new ExpenseHistory
        {
            ExpenseId = expense.Id,
            Action = action,
            ActorId = userId,
            OccurredAtUtc = now,
            FromStatus = previous,
            ToStatus = next,
            Justification = justification,
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("O reembolso foi alterado por outra operação. Tente novamente.");
        }

        return new ExpenseDecisionResponse(expense.Id, expense.Status, expense.RejectionReason, AsUtc(expense.UpdatedAtUtc));
    }

    private IQueryable<Expense> VisibleExpenses(IQueryable<Expense> source, string userId)
    {
        if (currentUser.IsInRole(Roles.Auditor))
        {
            return source;
        }

        bool isEmployee = currentUser.IsInRole(Roles.Employee);
        bool isApprover = currentUser.IsInRole(Roles.Approver);
        bool isFinance = currentUser.IsInRole(Roles.Finance);

        return source.Where(expense =>
            (isEmployee && expense.OwnerId == userId)
            || (isApprover && expense.Status == ExpenseStatus.Submitted)
            || (isFinance && (expense.Status == ExpenseStatus.Approved || expense.Status == ExpenseStatus.Paid)));
    }

    private async Task<Expense> LoadForDecisionAsync(Guid id, string userId, CancellationToken ct)
    {
        Expense? expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null || (expense.Status == ExpenseStatus.Draft && expense.OwnerId != userId))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        if (expense.OwnerId == userId)
        {
            throw new ForbiddenException("Não é permitido decidir ou pagar o próprio reembolso.");
        }

        return expense;
    }

    private string RequireUserId()
        => currentUser.UserId ?? throw new UnauthorizedException("Usuário não autenticado.");

    private void RequireRole(string role)
    {
        if (!currentUser.IsInRole(role))
        {
            throw new ForbiddenException($"A operação exige a role {role}.");
        }
    }

    private void RequireAnyRole(string[] roles)
    {
        if (!roles.Any(currentUser.IsInRole))
        {
            throw new ForbiddenException("O usuário não possui role com acesso a esta operação.");
        }
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private static void EnsureStatus(Expense expense, ExpenseStatus expected)
    {
        if (expense.Status != expected)
        {
            throw new ConflictException($"Operação inválida para o estado atual ({expense.Status}).");
        }
    }

    private static string NormalizeText(string value, string label)
    {
        string text = value.Trim();
        if (text.Length is < MinTextLength or > MaxTextLength)
        {
            throw new BadRequestException($"{label} deve ter entre {MinTextLength} e {MaxTextLength} caracteres.");
        }

        return text;
    }

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
