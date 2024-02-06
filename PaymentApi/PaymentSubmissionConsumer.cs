using MassTransit;

namespace PaymentApi;

public class PaymentSubmissionConsumer(IPublishEndpoint publishEndpoint, ILogger<PaymentSubmissionConsumer> logger)
{
    public async Task SubmitPayment(ConsumeContext<PaymentApproved> context)
    {
        logger.LogInformation("Submitting payment: {PaymentId} - approved with reason: {ApprovalReason}", context.Message.PaymentId, context.Message.Reason);

        await publishEndpoint.Publish(new PaymentSubmitted
            { PaymentId = context.Message.PaymentId, SubmittedOn = DateTime.Now, Reference = "Payment is successful, yay!" });
    }
}