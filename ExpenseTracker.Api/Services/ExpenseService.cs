using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.DTOs;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class ExpenseService(AppDbContext db)
{
    private IQueryable<ExpenseResponse> Responses(IQueryable<Expense> query) => query
        .Select(e => new ExpenseResponse(e.Id, e.Description, e.Amount, e.Date,
            e.CategoryId, e.Category.Name, e.PaymentMethod));

    public async Task<List<ExpenseResponse>> ListAsync(DateOnly? from, DateOnly? to, int? categoryId, CancellationToken ct)
    {
        if (from > to || categoryId <= 0)
            throw new ApiException(400, "Filtros inválidos: verifique o intervalo e a categoria.");
        var query = db.Expenses.AsNoTracking();
        if (from.HasValue) query = query.Where(e => e.Date >= from.Value);
        if (to.HasValue) query = query.Where(e => e.Date <= to.Value);
        if (categoryId.HasValue) query = query.Where(e => e.CategoryId == categoryId.Value);
        return await Responses(query.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)).ToListAsync(ct);
    }

    public async Task<ExpenseResponse> GetAsync(int id, CancellationToken ct) =>
        await Responses(db.Expenses.AsNoTracking().Where(e => e.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw new ApiException(404, "Despesa não encontrada.");

    public async Task<ExpenseResponse> CreateAsync(ExpenseRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, ct);
        var expense = new Expense();
        Apply(expense, request);
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return await GetAsync(expense.Id, ct);
    }

    public async Task UpdateAsync(int id, ExpenseRequest request, CancellationToken ct)
    {
        var expense = await db.Expenses.FindAsync([id], ct)
            ?? throw new ApiException(404, "Despesa não encontrada.");
        await ValidateAsync(request, ct);
        Apply(expense, request);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var expense = await db.Expenses.FindAsync([id], ct)
            ?? throw new ApiException(404, "Despesa não encontrada.");
        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MonthlyReportResponse> ReportAsync(int year, int month, CancellationToken ct)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12)
            throw new ApiException(400, "Ano ou mês inválido.");
        var start = new DateOnly(year, month, 1);
        var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var totals = await db.Expenses.AsNoTracking().Where(e => e.Date >= start && e.Date <= end)
            .GroupBy(e => new { e.CategoryId, e.Category.Name })
            .OrderBy(g => g.Key.Name)
            .Select(g => new CategoryTotalResponse(g.Key.CategoryId, g.Key.Name, g.Sum(e => e.Amount)))
            .ToListAsync(ct);
        return new MonthlyReportResponse(year, month, totals.Sum(g => g.Total), totals);
    }

    private async Task ValidateAsync(ExpenseRequest request, CancellationToken ct)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new ApiException(400, string.Join(" ", errors.Select(e => e.ErrorMessage)));
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            throw new ApiException(400, "CategoryId deve corresponder a uma categoria existente.");
    }

    private static void Apply(Expense expense, ExpenseRequest request)
    {
        expense.Description = request.Description.Trim();
        expense.Amount = request.Amount;
        expense.Date = request.Date;
        expense.CategoryId = request.CategoryId;
        expense.PaymentMethod = request.PaymentMethod.Trim();
    }
}
