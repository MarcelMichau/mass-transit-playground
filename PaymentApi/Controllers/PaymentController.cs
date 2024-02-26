using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentApi.Domain;
using PaymentApi.Events;
using PaymentApi.Persistence;

namespace PaymentApi.Controllers;

[ApiController]
[Route("[controller]")]
public class PaymentController(
    PaymentDbContext context,
    IPublishEndpoint publishEndpoint,
    IEventHubProducerProvider producerProvider,
    ILogger<PaymentController> logger) : ControllerBase
{
    [HttpPost(Name = "CreatePayment")]
    public async Task<IActionResult> CreatePayment(PaymentRequestModel paymentRequest)
    {
        var creationTimestamp = DateTime.Now;

        var payment = new Payment
        {
            Id = NewId.NextGuid(),
            CreatedOn = creationTimestamp,
            Amount = paymentRequest.Amount,
            FromAccountNumber = paymentRequest.FromAccountNumber,
            ToAccountNumber = paymentRequest.ToAccountNumber
        };

        context.Payments.Add(payment);

        var paymentCreatedEvent = new PaymentCreated
        {
            PaymentId = payment.Id,
            CreatedOn = creationTimestamp,
            Amount = payment.Amount,
            FromAccountNumber = payment.FromAccountNumber,
            ToAccountNumber = payment.ToAccountNumber
        };

        await publishEndpoint.Publish(paymentCreatedEvent);

        await context.SaveChangesAsync();

        var producer = await producerProvider.GetProducer("eh-masstransit-test");
        await producer.Produce(paymentCreatedEvent);

        logger.LogInformation("Payment created: {PaymentId}", payment.Id);

        return Ok(payment);
    }

    [HttpPost("approve", Name = "ApprovePayment")]
    public async Task<IActionResult> ApprovePayment(Guid paymentId, string reason)
    {
        var payment = await context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            return NotFound();

        await publishEndpoint.Publish(new PaymentApproved
        {
            PaymentId = payment.Id,
            ApprovedOn = DateTime.Now,
            Reason = reason
        });

        await context.SaveChangesAsync();

        logger.LogInformation("Payment approved: {PaymentId} with reason - {Reason}", payment.Id, reason.Replace(Environment.NewLine, ""));

        return Ok(payment);
    }
}

public class PaymentRequestModel
{
    public decimal Amount { get; init; }
    public string FromAccountNumber { get; init; }
    public string ToAccountNumber { get; init; }
}