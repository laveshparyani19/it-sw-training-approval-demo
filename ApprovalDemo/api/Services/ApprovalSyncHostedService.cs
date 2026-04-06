using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApprovalDemo.Api.Services
{
    public sealed class ApprovalSyncHostedService : BackgroundService
    {
        private readonly ApprovalSyncService _syncService;
        private readonly ILogger<ApprovalSyncHostedService> _logger;

        public ApprovalSyncHostedService(ApprovalSyncService syncService, ILogger<ApprovalSyncHostedService> logger)
        {
            _syncService = syncService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Approval sync worker starting.");

            await _syncService.InitializeAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _syncService.RunOnceAsync(stoppingToken);
                    if (result.Enabled)
                    {
                        _logger.LogInformation(
                            "Sync run complete. Processed={Processed}, Successful={Successful}, Failed={Failed}, WatermarkTo={WatermarkToUtc:o}",
                            result.Processed,
                            result.Successful,
                            result.Failed,
                            result.WatermarkToUtc);
                    }
                    else
                    {
                        _logger.LogInformation("Sync skipped: {Message}", result.Message);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in sync worker loop.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            _logger.LogInformation("Approval sync worker stopped.");
        }
    }
}
