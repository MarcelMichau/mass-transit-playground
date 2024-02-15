namespace PaymentApi.Messages;

public record SendPaymentApprovalNotification
{
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
}
