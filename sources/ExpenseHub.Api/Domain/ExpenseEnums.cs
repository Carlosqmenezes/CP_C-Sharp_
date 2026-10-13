namespace ExpenseHub.Api.Domain;

/// <summary>Estado do reembolso.</summary>
public enum ExpenseStatus
{
    /// <summary>Rascunho.</summary>
    Draft = 1,

    /// <summary>Enviado.</summary>
    Submitted = 2,

    /// <summary>Aprovado.</summary>
    Approved = 3,

    /// <summary>Reprovado.</summary>
    Rejected = 4,

    /// <summary>Pago.</summary>
    Paid = 5,
}

/// <summary>Ação registrada no histórico.</summary>
public enum ExpenseAction
{
    /// <summary>Criação.</summary>
    Created = 1,

    /// <summary>Alteração do rascunho.</summary>
    Updated = 2,

    /// <summary>Envio.</summary>
    Submitted = 3,

    /// <summary>Aprovação.</summary>
    Approved = 4,

    /// <summary>Reprovação.</summary>
    Rejected = 5,

    /// <summary>Pagamento.</summary>
    Paid = 6,
}
