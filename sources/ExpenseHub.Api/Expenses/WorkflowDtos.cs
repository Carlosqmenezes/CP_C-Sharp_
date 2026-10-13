using System;
using System.ComponentModel.DataAnnotations;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Expenses;

internal sealed class RejectExpenseRequest
{
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Justification { get; init; } = string.Empty;
}

internal sealed record ExpenseDecisionResponse(Guid Id, ExpenseStatus Status, string? RejectionReason, DateTime UpdatedAtUtc);

internal sealed record HistoryEntryResponse(
    int Id,
    ExpenseAction Action,
    string ActorId,
    DateTime OccurredAtUtc,
    ExpenseStatus? FromStatus,
    ExpenseStatus ToStatus,
    string? Justification,
    string? Changes);
