using ApprovalDemo.Api.Data;
using ApprovalDemo.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalDemo.Api.Controllers;

[ApiController]
[Route("api/task8")]
public sealed class Task8Controller : ControllerBase
{
    private readonly Task8Repository _repository;

    public Task8Controller(Task8Repository repository)
    {
        _repository = repository;
    }

    /// <summary>Task 8 reporting (1–10). Paginated tabular data with optional search. Use useMssqlMirror on report 7 to read from mirrored SQL Server via the same logic as dbo.sp_Task8_ActiveStudentsDetail.</summary>
    [HttpGet("report/{reportId:int}")]
    public async Task<ActionResult<Task8ReportResponse>> GetReport(
        int reportId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] bool useMssqlMirror = false,
        CancellationToken cancellationToken = default)
    {
        if (reportId is < 1 or > 10)
        {
            return BadRequest(new { error = "reportId must be between 1 and 10." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        try
        {
            var result = await _repository.GetReportAsync(reportId, page, pageSize, search, useMssqlMirror, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
