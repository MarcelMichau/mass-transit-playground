using MassTransit;
using PaymentApi.Events;
using PaymentApi.Messages;

namespace PaymentApi.Consumers;

public class PaymentProviderPollingConsumer(IPublishEndpoint publishEndpoint, IMessageScheduler messageScheduler, ILogger<PaymentProviderPollingConsumer> logger) : IConsumer<CheckPaymentStatus>
{
    public async Task Consume(ConsumeContext<CheckPaymentStatus> context)
    {
        var paymentStatus = await CheckPaymentStatus(context.Message.PaymentId);

        if (paymentStatus)
        {
            var paymentProcessed = new PaymentProcessed
            {
                PaymentId = context.Message.PaymentId,
                ProcessedOn = DateTime.UtcNow,
                Message = "Payment has been processed successfully"
            };

            logger.LogInformation("Payment {PaymentId} has been processed successfully", context.Message.PaymentId);

            await publishEndpoint.Publish(paymentProcessed);
        }
        else
        {
            var nextCheckDate = DateTime.UtcNow + TimeSpan.FromSeconds(10);

            logger.LogInformation("Payment {PaymentId} has not been processed yet - will retry at {NextCheckDate}", context.Message.PaymentId, nextCheckDate);

            await messageScheduler.SchedulePublish(nextCheckDate,
                new CheckPaymentStatus
                {
                    PaymentId = context.Message.PaymentId
                });
        }
    }

    private async Task<bool> CheckPaymentStatus(Guid paymentId)
    {
        logger.LogInformation("Checking payment status for payment: {PaymentId}", paymentId);

        await Task.Delay(500);

        var result = Random.Shared.Next(1, 100) <= 30;
        return result;
    }
}
