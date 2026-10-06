using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.DTOs;

public sealed class ExpenseRequest : IValidatableObject
{
    [Required, StringLength(200)] public string Description { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] public decimal Amount { get; init; }
    public DateOnly Date { get; init; }
    [Range(1, int.MaxValue)] public int CategoryId { get; init; }
    [Required, StringLength(50)] public string PaymentMethod { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date == default)
            yield return new ValidationResult("Date deve ser informada e válida.", [nameof(Date)]);
        if (decimal.Round(Amount, 2) != Amount)
            yield return new ValidationResult("Amount deve ter no máximo duas casas decimais.", [nameof(Amount)]);
    }
}

public sealed record ExpenseResponse(int Id, string Description, decimal Amount, DateOnly Date,
    int CategoryId, string CategoryName, string PaymentMethod);
public sealed record CategoryTotalResponse(int CategoryId, string CategoryName, decimal Total);
public sealed record MonthlyReportResponse(int Year, int Month, decimal Total,
    IReadOnlyList<CategoryTotalResponse> Categories);
