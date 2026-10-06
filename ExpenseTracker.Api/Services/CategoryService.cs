using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.DTOs;
using ExpenseTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Services;

public sealed class CategoryService(AppDbContext db)
{
    public Task<List<CategoryResponse>> ListAsync(CancellationToken ct) => db.Categories.AsNoTracking()
        .OrderBy(c => c.Name).Select(c => new CategoryResponse(c.Id, c.Name)).ToListAsync(ct);

    public async Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken ct)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new ApiException(400, "Name é obrigatório e deve ter até 100 caracteres.");
        var category = new Category { Name = request.Name.Trim() };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return new CategoryResponse(category.Id, category.Name);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([id], ct)
            ?? throw new ApiException(404, "Categoria não encontrada.");
        if (await db.Expenses.AnyAsync(e => e.CategoryId == id, ct))
            throw new ApiException(409, "A categoria está sendo utilizada por uma despesa.");
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }
}
