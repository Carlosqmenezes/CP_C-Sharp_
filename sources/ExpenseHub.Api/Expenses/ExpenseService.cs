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

internal sealed class ExpenseService(AppDbContext db, CurrentUser currentUser, TimeProvider clock)
{
    private const string NotFoundMessage = "Reembolso não encontrado.";
    private const int MinTextLength = 10;
    private const int MaxTextLength = 500;

    private static readonly string[] ReaderRoles = [Roles.Employee, Roles.Approver, Roles.Finance, Roles.Auditor];

    public async Task<ExpenseResponse> CreateAsync(ExpenseRequest request, CancellationToken ct = default)
    {
        string userId = RequireUserId();
        RequireRole(Roles.Employee);
        RequestValidator.Validate(request);
        string description = NormalizeText(request.Description);
        await EnsureCategoryExistsAsync(request.CategoryId, ct);

        DateTime now = Now();
        Expense expense = new()
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Description = description,
            Amount = request.Amount,
            ExpenseDate = request.ExpenseDate,
            CategoryId = request.CategoryId,
            Status = ExpenseStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Expenses.Add(expense);
        db.ExpenseHistories.Add(NewHistory(expense.Id, ExpenseAction.Created, userId, now, null, ExpenseStatus.Draft, null));
        await SaveAsync(ct);
        return ToResponse(expense);
    }

    public async Task<ExpenseResponse> UpdateAsync(Guid id, ExpenseRequest request, CancellationToken ct = default)
    {
        string userId = RequireUserId();
        RequireRole(Roles.Employee);
        RequestValidator.Validate(request);
        string description = NormalizeText(request.Description);

        Expense expense = await LoadOwnedAsync(id, userId, ct);
        EnsureStatus(expense, ExpenseStatus.Draft);
        await EnsureCategoryExistsAsync(request.CategoryId, ct);

        List<string> changes = [];
        Track(changes, "Descrição", expense.Description, description);
        Track(changes, "Valor", expense.Amount, request.Amount);
        Track(changes, "Data", expense.ExpenseDate, request.ExpenseDate);
        Track(changes, "Categoria", expense.CategoryId, request.CategoryId);
        if (changes.Count == 0)
        {
            return ToResponse(expense);
        }

        DateTime now = Now();
        expense.Description = description;
        expense.Amount = request.Amount;
        expense.ExpenseDate = request.ExpenseDate;
        expense.CategoryId = request.CategoryId;
        expense.UpdatedAtUtc = now;

        db.ExpenseHistories.Add(NewHistory(
            expense.Id, ExpenseAction.Updated, userId, now, ExpenseStatus.Draft, ExpenseStatus.Draft, string.Join("; ", changes)));
        await SaveAsync(ct);
        return ToResponse(expense);
    }

    public async Task<IReadOnlyList<ExpenseResponse>> ListAsync(CancellationToken ct = default)
    {
        string userId = RequireUserId();
        RequireAnyRole(ReaderRoles);

        List<Expense> expenses = await VisibleExpenses(db.Expenses.AsNoTracking(), userId)
            .OrderByDescending(expense => expense.CreatedAtUtc)
            .ToListAsync(ct);

        return expenses.Select(ToResponse).ToList();
    }

    public async Task<ExpenseResponse> GetAsync(Guid id, CancellationToken ct = default)
    {
        string userId = RequireUserId();
        RequireAnyRole(ReaderRoles);

        Expense expense = await VisibleExpenses(db.Expenses.AsNoTracking(), userId).FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(NotFoundMessage);
        return ToResponse(expense);
    }

    public async Task<ExpenseResponse> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        string userId = RequireUserId();
        RequireRole(Roles.Employee);

        Expense expense = await LoadOwnedAsync(id, userId, ct);
        EnsureStatus(expense, ExpenseStatus.Draft);

        DateTime now = Now();
        expense.Status = ExpenseStatus.Submitted;
        expense.UpdatedAtUtc = now;
        db.ExpenseHistories.Add(NewHistory(
            expense.Id, ExpenseAction.Submitted, userId, now, ExpenseStatus.Draft, ExpenseStatus.Submitted, null));
        await SaveAsync(ct);
        return ToResponse(expense);
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

    private async Task<Expense> LoadOwnedAsync(Guid id, string userId, CancellationToken ct)
    {
        Expense expense = await VisibleExpenses(db.Expenses, userId).FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException(NotFoundMessage);

        if (expense.OwnerId != userId)
        {
            throw new ForbiddenException("Apenas o proprietário pode executar esta operação.");
        }

        return expense;
    }

    private async Task EnsureCategoryExistsAsync(int? categoryId, CancellationToken ct)
    {
        if (categoryId is null)
        {
            return;
        }

        if (!await db.ExpenseCategories.AnyAsync(category => category.Id == categoryId, ct))
        {
            throw new BadRequestException("Categoria inexistente.");
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("O reembolso foi alterado por outra operação. Tente novamente.");
        }
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

    private static string NormalizeText(string value)
    {
        string text = value.Trim();
        if (text.Length is < MinTextLength or > MaxTextLength)
        {
            throw new BadRequestException($"A descrição deve ter entre {MinTextLength} e {MaxTextLength} caracteres.");
        }

        return text;
    }

    private static void Track<T>(List<string> changes, string field, T before, T after)
    {
        if (!EqualityComparer<T>.Default.Equals(before, after))
        {
            changes.Add(FormattableString.Invariant($"{field}: '{before}' -> '{after}'"));
        }
    }

    private static ExpenseHistory NewHistory(
        Guid expenseId, ExpenseAction action, string actorId, DateTime occurredAtUtc,
        ExpenseStatus? from, ExpenseStatus to, string? changes)
        => new()
        {
            ExpenseId = expenseId,
            Action = action,
            ActorId = actorId,
            OccurredAtUtc = occurredAtUtc,
            FromStatus = from,
            ToStatus = to,
            Changes = changes,
        };

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static ExpenseResponse ToResponse(Expense e) => new(
        e.Id, e.OwnerId, e.Description, e.Amount, e.ExpenseDate, e.CategoryId, e.Status,
        e.RejectionReason, AsUtc(e.CreatedAtUtc), AsUtc(e.UpdatedAtUtc));
}
