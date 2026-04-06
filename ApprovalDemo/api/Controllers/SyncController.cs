using System.Threading;
using System.Threading.Tasks;
using ApprovalDemo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalDemo.Api.Controllers
{
    [ApiController]
    [Route("api/sync")]
    public class SyncController : ControllerBase
    {
        private readonly ApprovalSyncService _syncService;

        public SyncController(ApprovalSyncService syncService)
        {
            _syncService = syncService;
        }

        [HttpPost("run")]
        public async Task<IActionResult> RunNow(CancellationToken cancellationToken)
        {
            var result = await _syncService.RunOnceAsync(cancellationToken);
            return Ok(result);
        }

        [HttpPost("reconcile")]
        public async Task<IActionResult> ReconcileNow(CancellationToken cancellationToken)
        {
            var result = await _syncService.RunReconciliationNowAsync(cancellationToken);
            return Ok(result);
        }
    }
}
