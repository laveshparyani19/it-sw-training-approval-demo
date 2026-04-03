using Microsoft.AspNetCore.Mvc;
using ApprovalDemo.Api.Data;
using ApprovalDemo.Api.Models;
using System.Threading.Tasks;

namespace ApprovalDemo.Api.Controllers
{
    [ApiController]
    [Route("api/approval-requests")]
    public class ApprovalController : ControllerBase
    {
        private readonly ApprovalRepository _repository;

        public ApprovalController(ApprovalRepository repository)
        {
            _repository = repository;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateRequestDto dto)
        {
            var id = await _repository.CreateRequestAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id }, new { id, dto.Title });
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            var requests = await _repository.GetPendingRequestsAsync();
            return Ok(requests);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var request = await _repository.GetByIdAsync(id);
            if (request == null) return NotFound();
            return Ok(request);
        }

        [HttpPost("{id}/approve")]
        public async Task<IActionResult> Approve(int id, [FromBody] DecisionDto dto)
        {
            Console.WriteLine($"Controller: Received Approve for {id}");
            var request = await _repository.GetByIdAsync(id);
            if (request == null) 
            {
                Console.WriteLine($"Controller: Request {id} not found");
                return NotFound();
            }
            if (request.Status != 0) 
            {
                Console.WriteLine($"Controller: Request {id} is already processed (Status: {request.Status})");
                return BadRequest("Request is already processed.");
            }

            var rowsAffected = await _repository.ApproveRequestAsync(id, dto.DecisionBy);
            if (rowsAffected == 0)
            {
                Console.WriteLine($"Controller: WARNING - No rows updated for request {id}");
                return StatusCode(500, "Failed to update status in database.");
            }

            Console.WriteLine($"Controller: Successfully approved {id}");
            return Ok(new { message = "Request approved successfully." });
        }

        [HttpPost("{id}/reject")]
        public async Task<IActionResult> Reject(int id, [FromBody] DecisionDto dto)
        {
            Console.WriteLine($"Controller: Received Reject for {id}");
            var request = await _repository.GetByIdAsync(id);
            if (request == null)
            {
                Console.WriteLine($"Controller: Request {id} not found");
                return NotFound();
            }
            if (request.Status != 0)
            {
                Console.WriteLine($"Controller: Request {id} is already processed (Status: {request.Status})");
                return BadRequest("Request is already processed.");
            }
            if (string.IsNullOrEmpty(dto.RejectReason)) return BadRequest("Reject reason is required.");

            var rowsAffected = await _repository.RejectRequestAsync(id, dto.DecisionBy, dto.RejectReason);
            if (rowsAffected == 0)
            {
                Console.WriteLine($"Controller: WARNING - No rows updated for request {id}");
                return StatusCode(500, "Failed to update status in database.");
            }

            Console.WriteLine($"Controller: Successfully rejected {id}");
            return Ok(new { message = "Request rejected successfully." });
        }
    }
}
