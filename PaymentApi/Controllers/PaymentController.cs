using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PaymentApi.Controllers;

[ApiController]
[Route("[controller]")]
public class PaymentController(
    PaymentDbContext context,
    IPublishEndpoint publishEndpoint,
    ILogger<PaymentController> logger) : ControllerBase
{
    [HttpPost(Name = "CreatePayment")]
    public async Task<IActionResult> CreatePayment(PaymentRequestModel paymentRequest)
    {
        var payment = new Payment
        {
            Id = NewId.NextGuid(),
            CreatedOn = DateTime.Now,
            Amount = paymentRequest.Amount,
            FromAccountNumber = paymentRequest.FromAccountNumber,
            ToAccountNumber = paymentRequest.ToAccountNumber
        };

        context.Payments.Add(payment);

        await publishEndpoint.Publish(new PaymentCreated
        {
            PaymentId = payment.Id,
            Amount = payment.Amount,
            FromAccountNumber = payment.FromAccountNumber,
            ToAccountNumber = payment.ToAccountNumber
        });

        await context.SaveChangesAsync();

        logger.LogInformation("Payment created: {PaymentId}", payment.Id);

        return Ok(payment);
    }

    [HttpPost("approve", Name = "ApprovePayment")]
    public async Task<IActionResult> ApprovePayment(Guid paymentId)
    {
        var payment = await context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment is null)
            return NotFound();

        await publishEndpoint.Publish(new PaymentApproved
        {
            PaymentId = payment.Id,
            ApprovedOn = DateTime.Now,
            Reason = "Payment approved as amount is less than 2000"
        });

        await context.SaveChangesAsync();

        logger.LogInformation("Payment approved: {PaymentId}", payment.Id);

        return Ok(payment);
    }
}