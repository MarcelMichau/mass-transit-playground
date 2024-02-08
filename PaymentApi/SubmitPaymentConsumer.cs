using MassTransit;

namespace PaymentApi;

public class SubmitPaymentConsumer(IPublishEndpoint publishEndpoint, ILogger<SubmitPaymentConsumer> logger) : IConsumer<SubmitPayment>
{
    public async Task Consume(ConsumeContext<SubmitPayment> context)
    {
        logger.LogInformation("Submitting payment: {PaymentId} - approved with reason: {ApprovalReason}", context.Message.PaymentId, context.Message.DecisionReason);

        await publishEndpoint.Publish(new PaymentSubmitted
            { PaymentId = context.Message.PaymentId, SubmittedOn = DateTime.Now, Reference = "Payment is successful, yay!" });
    }
}