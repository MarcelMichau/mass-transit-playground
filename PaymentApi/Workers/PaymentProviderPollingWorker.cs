using MassTransit;
using PaymentApi.Events;
using PaymentApi.Persistence;

namespace PaymentApi.Workers;

public class PaymentProviderPollingWorker(IServiceScopeFactory serviceScopeFactory, ILogger<PaymentProviderPollingWorker> logger)
    : BackgroundService
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(5));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = serviceScopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            var submittedPayments =
                context.PaymentState.Where(s => s.CurrentState == "AwaitingProcessingConfirmation").ToList();

            logger.LogInformation("Found {Count} payments in AwaitingProcessingConfirmation state", submittedPayments.Count);

            foreach (var submittedPayment in submittedPayments)
            {
                var paymentStatus = await CheckPaymentStatus(submittedPayment.CorrelationId);

                if (!paymentStatus) continue;

                var paymentProcessed = new PaymentProcessed
                {
                    PaymentId = submittedPayment.CorrelationId,
                    ProcessedOn = DateTime.UtcNow,
                    Message = "Payment has been processed successfully"
                };

                await publishEndpoint.Publish(paymentProcessed, stoppingToken);

                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }

    private async Task<bool> CheckPaymentStatus(Guid paymentId)
    {
        await Task.Delay(500);

        var result = Random.Shared.Next(1, 100) <= 30;
        if (result)
        {
            logger.LogInformation("Payment {PaymentId} has been processed successfully", paymentId);
            return true;
        }

        logger.LogInformation("Payment {PaymentId} has not been processed yet - will retry", paymentId);

        return false;
    }
}
