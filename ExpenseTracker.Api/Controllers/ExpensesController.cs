using ExpenseTracker.Api.DTOs;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController(ExpenseService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ExpenseResponse>>> List([FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, [FromQuery] int? categoryId, CancellationToken ct) =>
        Ok(await service.ListAsync(from, to, categoryId, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpenseResponse>> Get(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> Create(ExpenseRequest request, CancellationToken ct)
    {
        var expense = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = expense.Id }, expense);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ExpenseRequest request, CancellationToken ct)
    {
        await service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("reports/monthly")]
    public async Task<ActionResult<MonthlyReportResponse>> Report([FromQuery] int year,
        [FromQuery] int month, CancellationToken ct) => Ok(await service.ReportAsync(year, month, ct));
}
