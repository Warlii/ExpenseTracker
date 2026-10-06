using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.DTOs;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpenseTracker.Api.Tests;

public sealed class BusinessRulesTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly AppDbContext db;
    private readonly ExpenseService expenses;

    public BusinessRulesTests()
    {
        connection.Open();
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        db.Categories.AddRange(new Category { Id = 1, Name = "Alimentação" }, new Category { Id = 2, Name = "Transporte" });
        db.SaveChanges();
        expenses = new ExpenseService(db);
    }

    private static ExpenseRequest Request(decimal amount = 10, int categoryId = 1,
        string description = "Almoço", string paymentMethod = "Pix", DateOnly? date = null) => new()
    {
        Description = description, Amount = amount, CategoryId = categoryId,
        PaymentMethod = paymentMethod, Date = date ?? new DateOnly(2026, 10, 5)
    };

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(0.001)] [InlineData(1.999)]
    public async Task RejectsInvalidAmount(decimal amount)
    {
        var error = await Assert.ThrowsAsync<ApiException>(() => expenses.CreateAsync(Request(amount), default));
        Assert.Equal(400, error.StatusCode);
        Assert.Empty(await db.Expenses.ToListAsync());
    }

    [Theory]
    [InlineData("", "Pix")] [InlineData("   ", "Pix")] [InlineData("Almoço", " ")]
    public async Task RejectsMissingRequiredText(string description, string paymentMethod)
    {
        var error = await Assert.ThrowsAsync<ApiException>(() => expenses.CreateAsync(
            Request(description: description, paymentMethod: paymentMethod), default));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task RejectsMissingDateAndUnknownCategory()
    {
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => expenses.CreateAsync(
            Request(date: default(DateOnly)), default))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => expenses.CreateAsync(
            Request(categoryId: 999), default))).StatusCode);
    }

    [Fact]
    public async Task UsedCategoryCannotBeDeleted()
    {
        await expenses.CreateAsync(Request(), default);
        var error = await Assert.ThrowsAsync<ApiException>(() => new CategoryService(db).DeleteAsync(1, default));
        Assert.Equal(409, error.StatusCode);
        Assert.NotNull(await db.Categories.FindAsync(1));
    }

    [Fact]
    public async Task UnusedCategoryCanBeDeleted()
    {
        await new CategoryService(db).DeleteAsync(2, default);
        Assert.Null(await db.Categories.FindAsync(2));
    }

    [Fact]
    public async Task UpdateValidatesCategoryAndPreservesOriginalOnFailure()
    {
        var created = await expenses.CreateAsync(Request(), default);
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => expenses.UpdateAsync(
            created.Id, Request(20, 999), default))).StatusCode);
        Assert.Equal(10, (await expenses.GetAsync(created.Id, default)).Amount);
        await expenses.UpdateAsync(created.Id, Request(20, 2), default);
        Assert.Equal(2, (await expenses.GetAsync(created.Id, default)).CategoryId);
        await expenses.DeleteAsync(created.Id, default);
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => expenses.GetAsync(created.Id, default))).StatusCode);
    }

    [Fact]
    public async Task FiltersIncludeBothDateBoundariesAndCategory()
    {
        await expenses.CreateAsync(Request(date: new DateOnly(2026, 10, 1)), default);
        await expenses.CreateAsync(Request(date: new DateOnly(2026, 10, 31)), default);
        await expenses.CreateAsync(Request(categoryId: 2), default);
        await expenses.CreateAsync(Request(date: new DateOnly(2026, 11, 1)), default);
        var result = await expenses.ListAsync(new(2026, 10, 1), new(2026, 10, 31), 1, default);
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal(1, e.CategoryId));
    }

    [Fact]
    public async Task InvalidFiltersAndPeriodReturnBadRequest()
    {
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => expenses.ListAsync(
            new(2026, 11, 1), new(2026, 10, 1), null, default))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => expenses.ReportAsync(2026, 13, default))).StatusCode);
    }

    [Fact]
    public async Task EmptyMonthReturnsZero()
    {
        var result = await expenses.ReportAsync(9999, 12, default);
        Assert.Equal(0, result.Total);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task ReportAggregatesOnlyRequestedMonth()
    {
        await expenses.CreateAsync(Request(10), default);
        await expenses.CreateAsync(Request(20), default);
        await expenses.CreateAsync(Request(5, 2), default);
        await expenses.CreateAsync(Request(100, date: new DateOnly(2026, 11, 1)), default);
        var result = await expenses.ReportAsync(2026, 10, default);
        Assert.Equal(35, result.Total);
        Assert.Equal(30, result.Categories.Single(c => c.CategoryId == 1).Total);
        Assert.Equal(5, result.Categories.Single(c => c.CategoryId == 2).Total);
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
