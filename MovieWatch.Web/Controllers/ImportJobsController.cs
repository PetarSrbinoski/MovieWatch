using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieWatch.Web.Mapper;
using MovieWatch.Web.Request;
using MovieWatch.Web.Response;

namespace MovieWatch.Web.Controllers;

[ApiController, Authorize(Roles = "Administrator"), Route("api/import-jobs")]
public sealed class ImportJobsController(ImportJobMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ImportJobResponse>>> List(int skip = 0, int take = 20,
        CancellationToken cancellationToken = default)
        => Ok(await mapper.ListAsync(skip, take, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ImportJobResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await mapper.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ImportJobResponse>> Create(CreateImportJobRequest request, CancellationToken cancellationToken)
    {
        var job = await mapper.CreateAsync(request, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id = job.Id }, job);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ImportJobResponse>> Update(Guid id, UpdateImportJobRequest request,
        CancellationToken cancellationToken)
        => Ok(await mapper.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, int version, CancellationToken cancellationToken)
    {
        await mapper.DeleteAsync(id, version, cancellationToken);
        return NoContent();
    }
}
