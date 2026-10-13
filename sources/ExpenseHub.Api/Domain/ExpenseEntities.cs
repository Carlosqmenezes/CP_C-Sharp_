using System;

namespace ExpenseHub.Api.Domain;

internal class ExpenseCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal class Expense
{
    public Guid Id { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public int? CategoryId { get; set; }

    public ExpenseStatus Status { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

internal class ExpenseHistory
{
    public int Id { get; set; }

    public Guid ExpenseId { get; set; }

    public ExpenseAction Action { get; set; }

    public string ActorId { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public ExpenseStatus? FromStatus { get; set; }

    public ExpenseStatus ToStatus { get; set; }

    public string? Justification { get; set; }

    public string? Changes { get; set; }
}

internal class PaymentRecord
{
    public Guid Id { get; set; }

    public Guid ExpenseId { get; set; }

    public string PaidByUserId { get; set; } = string.Empty;

    public DateTime PaidAtUtc { get; set; }

    public decimal Amount { get; set; }
}
