using MassTransit;
using PaymentApi.Events;

namespace PaymentApi.Consumers;

public class IntegrationEventPublisherConsumer(ILogger<IntegrationEventPublisherConsumer> logger) :
    IConsumer<PaymentCreated>,
    IConsumer<PaymentApproved>,
    IConsumer<PaymentRejected>,
    IConsumer<PaymentSubmitted>,
    IConsumer<PaymentProcessed>
{
    public async Task Consume(ConsumeContext<PaymentCreated> context)
    {
        logger.LogInformation("I am publishing an integration event for PaymentCreated - Payment ID: {PaymentId}", context.Message.PaymentId);
    }

    public async Task Consume(ConsumeContext<PaymentApproved> context)
    {
        logger.LogInformation("I am publishing an integration event for PaymentApproved - Payment ID: {PaymentId}", context.Message.PaymentId);
    }

    public async Task Consume(ConsumeContext<PaymentRejected> context)
    {
        logger.LogInformation("I am publishing an integration event for PaymentApproved - Payment ID: {PaymentId}", context.Message.PaymentId);
    }

    public async Task Consume(ConsumeContext<PaymentSubmitted> context)
    {
        logger.LogInformation("I am publishing an integration event for PaymentSubmitted - Payment ID: {PaymentId}", context.Message.PaymentId);
    }

    public async Task Consume(ConsumeContext<PaymentProcessed> context)
    {
        logger.LogInformation("I am publishing an integration event for PaymentProcessed - Payment ID: {PaymentId}", context.Message.PaymentId);
    }
}
