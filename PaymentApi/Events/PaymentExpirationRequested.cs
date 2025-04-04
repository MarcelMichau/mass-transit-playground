namespace PaymentApi.Events;

// New message for scheduled expiration
public record PaymentExpirationRequested
{
    public Guid PaymentId { get; init; }
}