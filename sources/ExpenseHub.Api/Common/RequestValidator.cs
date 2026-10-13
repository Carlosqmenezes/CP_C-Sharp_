using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ExpenseHub.Api.Common;

internal static class RequestValidator
{
    public static void Validate(object request)
    {
        List<ValidationResult> results = [];
        if (Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            return;
        }

        throw new BadRequestException(string.Join(" ", results.Select(result => result.ErrorMessage)));
    }
}
