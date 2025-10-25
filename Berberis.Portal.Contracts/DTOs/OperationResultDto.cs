namespace Berberis.Portal.Contracts.DTOs;

/// <summary>Result of a control operation.</summary>
public class OperationResultDto
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if operation failed.</summary>
    public string? Error { get; set; }

    /// <summary>Additional details or context.</summary>
    public string? Message { get; set; }
}
