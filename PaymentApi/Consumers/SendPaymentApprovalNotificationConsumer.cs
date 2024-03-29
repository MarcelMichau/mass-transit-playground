﻿using MassTransit;
using PaymentApi.Messages;

namespace PaymentApi.Consumers;

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