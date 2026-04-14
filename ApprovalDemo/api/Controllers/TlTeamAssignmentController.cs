using ApprovalDemo.Api.Data;
using ApprovalDemo.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalDemo.Api.Controllers
{
    [ApiController]
    [Route("api/tl")]
    public sealed class TlTeamAssignmentController : ControllerBase
    {
        private readonly StaffRepository _staffRepository;
        private readonly TlTeamAssignmentRepository _tlRepository;

        public TlTeamAssignmentController(StaffRepository staffRepository, TlTeamAssignmentRepository tlRepository)
        {
            _staffRepository = staffRepository;
            _tlRepository = tlRepository;
        }

        [HttpGet("team-options")]
        public async Task<IActionResult> GetTeamOptions([FromQuery] int limit = 100, CancellationToken cancellationToken = default)
        {
            var safeLimit = Math.Clamp(limit, 1, 300);
            var result = await _staffRepository.GetTeamOptionsAsync(safeLimit, cancellationToken);
            return Ok(result);
        }

        [HttpGet("team-members")]
        public async Task<IActionResult> GetTeamMembers(
            [FromQuery] string department,
            [FromQuery] string team,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(department) || string.IsNullOrWhiteSpace(team))
            {
                return BadRequest(new { error = "department and team are required." });
            }

            var query = new StaffDirectoryQuery
            {
                Department = department.Trim(),
                Team = team.Trim(),
                Search = search,
                Page = page,
                PageSize = Math.Clamp(pageSize, 1, 200),
                OnlyActive = true,
                ExcludeSystemAccounts = true
            };

            var result = await _staffRepository.GetStaffAsync(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("assignments")]
        public async Task<IActionResult> CreateAssignment([FromBody] CreateTlTeamAssignmentDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var distinctIds = dto.MemberStaffIds.Distinct().ToArray();
            if (distinctIds.Length != dto.MemberStaffIds.Length)
            {
                return BadRequest(new { error = "Duplicate staff ids are not allowed." });
            }

            var teamExists = await _staffRepository.GetTeamOptionsAsync(500, cancellationToken);
            var match = teamExists.Any(t =>
                string.Equals(t.DepartmentName, dto.DepartmentName.Trim(), StringComparison.Ordinal)
                && string.Equals(t.TeamName, dto.TeamName.Trim(), StringComparison.Ordinal));

            if (!match)
            {
                return BadRequest(new { error = "Selected team is not a valid staff team." });
            }

            var membersValid = await _tlRepository.StaffIdsExistAndMatchTeamAsync(
                distinctIds,
                dto.DepartmentName,
                dto.TeamName,
                cancellationToken);

            if (!membersValid)
            {
                return BadRequest(new { error = "One or more selected members are invalid or do not belong to the selected team." });
            }

            dto.MemberStaffIds = distinctIds;
            var created = await _tlRepository.CreateAsync(dto, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }

        [HttpGet("assignments")]
        public async Task<IActionResult> GetAssignments([FromQuery] string tlStaffCode, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tlStaffCode))
            {
                return BadRequest(new { error = "tlStaffCode is required." });
            }

            var list = await _tlRepository.ListRecentByTlAsync(tlStaffCode.Trim(), take, cancellationToken);
            return Ok(list);
        }
    }
}
