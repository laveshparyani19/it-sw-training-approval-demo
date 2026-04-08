using ApprovalDemo.Api.Data;
using ApprovalDemo.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalDemo.Api.Controllers
{
    [ApiController]
    [Route("api/students")]
    public sealed class StudentsController : ControllerBase
    {
        private readonly StudentRepository _repository;

        public StudentsController(StudentRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("grades")]
        public async Task<IActionResult> GetGrades([FromQuery] string? search, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 200);
            var result = await _repository.GetGradesAsync(search, safeLimit, cancellationToken);
            return Ok(result);
        }

        [HttpGet("sections")]
        public async Task<IActionResult> GetSections([FromQuery] string? grade, [FromQuery] string? search, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 200);
            var result = await _repository.GetSectionsAsync(grade, search, safeLimit, cancellationToken);
            return Ok(result);
        }

        [HttpGet("directory")]
        public async Task<IActionResult> GetDirectory([FromQuery] StudentDirectoryQuery query, CancellationToken cancellationToken = default)
        {
            query.Page = Math.Clamp(query.Page, 1, 1000);
            query.PageSize = Math.Clamp(query.PageSize, 1, 100);

            var result = await _repository.GetStudentsAsync(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("by-ids")]
        public async Task<IActionResult> GetByIds([FromQuery] string ids, CancellationToken cancellationToken = default)
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
                return Ok(Array.Empty<StudentDirectoryItem>());
            }

            var result = await _repository.GetStudentsByIdsAsync(parsedIds, cancellationToken);
            return Ok(result);
        }
    }
}
