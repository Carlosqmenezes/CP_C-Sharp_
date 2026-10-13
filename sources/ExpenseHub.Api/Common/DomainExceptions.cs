using System;
using Microsoft.AspNetCore.Http;

namespace ExpenseHub.Api.Common;

internal abstract class DomainException(string message, int statusCode, string title) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;
}

internal sealed class BadRequestException(string message)
    : DomainException(message, StatusCodes.Status400BadRequest, "Requisição inválida");

internal sealed class UnauthorizedException(string message)
    : DomainException(message, StatusCodes.Status401Unauthorized, "Não autenticado");

internal sealed class ForbiddenException(string message)
    : DomainException(message, StatusCodes.Status403Forbidden, "Acesso negado");

internal sealed class NotFoundException(string message)
    : DomainException(message, StatusCodes.Status404NotFound, "Recurso não encontrado");

internal sealed class ConflictException(string message)
    : DomainException(message, StatusCodes.Status409Conflict, "Conflito de estado");
