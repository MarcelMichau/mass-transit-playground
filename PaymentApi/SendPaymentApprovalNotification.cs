namespace PaymentApi;

public record SendPaymentApprovalNotification
{
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
}
