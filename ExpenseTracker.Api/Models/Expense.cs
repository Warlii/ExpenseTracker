namespace ExpenseTracker.Api.Models;

public class Expense
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string PaymentMethod { get; set; } = string.Empty;
}
