using ExpenseTracker.Api.DTOs;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(CategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryResponse>>> List(CancellationToken ct) =>
        Ok(await service.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryRequest request, CancellationToken ct)
    {
        var category = await service.CreateAsync(request, ct);
        return Created("/api/categories", category);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
