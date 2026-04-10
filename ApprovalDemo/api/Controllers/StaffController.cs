using ApprovalDemo.Api.Data;
using ApprovalDemo.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalDemo.Api.Controllers
{
    [ApiController]
    [Route("api/staff")]
    public sealed class StaffController : ControllerBase
    {
        private readonly StaffRepository _repository;

        public StaffController(StaffRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("departments")]
        public async Task<IActionResult> GetDepartments([FromQuery] string? search, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 200);
            var result = await _repository.GetDepartmentsAsync(search, safeLimit, cancellationToken);
            return Ok(result);
        }

        [HttpGet("teams")]
        public async Task<IActionResult> GetTeams([FromQuery] string? department, [FromQuery] string? departments, [FromQuery] string? search, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 200);
            var selectedDepartments = (departments ?? department ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static value => value.Trim())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var result = await _repository.GetTeamsAsync(selectedDepartments, search, safeLimit, cancellationToken);
            return Ok(result);
        }

        [HttpGet("directory")]
        public async Task<IActionResult> GetDirectory([FromQuery] StaffDirectoryQuery query, CancellationToken cancellationToken = default)
        {
            query.Page = Math.Clamp(query.Page, 1, 1000);
            query.PageSize = Math.Clamp(query.PageSize, 1, 100);

            var result = await _repository.GetStaffAsync(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("by-ids")]
        public async Task<IActionResult> GetByIds([FromQuery] string ids, [FromQuery] bool excludeSystemAccounts = true, CancellationToken cancellationToken = default)
        {
            var parsedIds = (ids ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static value => int.TryParse(value, out var id) ? id : -1)
                .Where(static id => id > 0)
                .Distinct()
                .Take(200)
                .ToArray();

            if (parsedIds.Length == 0)
            {
                return Ok(Array.Empty<StaffDirectoryItem>());
            }

            var result = await _repository.GetStaffByIdsAsync(parsedIds, excludeSystemAccounts, cancellationToken);
            return Ok(result);
        }
    }
}
