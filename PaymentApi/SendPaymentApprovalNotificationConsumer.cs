using MassTransit;

namespace PaymentApi;

public class SendPaymentApprovalNotificationConsumer(ILogger<SendPaymentApprovalNotificationConsumer> logger)
    : IConsumer<SendPaymentApprovalNotification>
{
    public Task Consume(ConsumeContext<SendPaymentApprovalNotification> context)
    {
        logger.LogInformation("Sending payment approval notification for payment: {PaymentId}",
            context.Message.PaymentId);

        return Task.CompletedTask;
    }
}