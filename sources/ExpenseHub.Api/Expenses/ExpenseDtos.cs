using System;
using System.ComponentModel.DataAnnotations;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Expenses;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class ExpenseDateAttribute() : ValidationAttribute("A data da despesa deve ser válida e não pode ser futura.")
{
    public override bool IsValid(object? value)
    {
        if (value is not DateOnly date)
        {
            return false;
        }

        return date != DateOnly.MinValue && date <= DateOnly.FromDateTime(DateTime.UtcNow);
    }
}

internal sealed class ExpenseRequest
{
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Description { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "2147483647", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal Amount { get; init; }

    [ExpenseDate]
    public DateOnly ExpenseDate { get; init; }

    public int? CategoryId { get; init; }
}

internal sealed record ExpenseResponse(
    Guid Id,
    string OwnerId,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate,
    int? CategoryId,
    ExpenseStatus Status,
    string? RejectionReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
