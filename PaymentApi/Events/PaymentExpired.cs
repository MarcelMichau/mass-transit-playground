namespace PaymentApi.Events;

public record PaymentExpired
{
    public Guid PaymentId { get; init; }
    public DateTime ExpiredOn { get; init; }
    public string Reason { get; init; } = "Payment expired due to timeout";
}