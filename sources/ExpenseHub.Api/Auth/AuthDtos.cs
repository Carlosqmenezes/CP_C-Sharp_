using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Auth;

internal sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Password { get; init; } = string.Empty;
}

internal sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string TokenType = "Bearer");
