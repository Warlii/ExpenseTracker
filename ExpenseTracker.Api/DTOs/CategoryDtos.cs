using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.DTOs;

public sealed class CategoryRequest
{
    [Required, StringLength(100)] public string Name { get; init; } = string.Empty;
}
public sealed record CategoryResponse(int Id, string Name);
