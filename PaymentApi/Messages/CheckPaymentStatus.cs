namespace PaymentApi.Messages;

public record CheckPaymentStatus
{
    public Guid PaymentId { get; init; }
}
