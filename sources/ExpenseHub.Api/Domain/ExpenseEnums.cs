

namespace ExpenseHub.Api.Domain;

public enum ExpenseStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Paid = 5,
}

public enum ExpenseAction
{
    Created = 1,
    Updated = 2,
    Submitted = 3,
    Approved = 4,
    Rejected = 5,
    Paid = 6,
}
