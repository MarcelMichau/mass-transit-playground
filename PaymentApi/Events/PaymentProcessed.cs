namespace PaymentApi.Events;

public record PaymentProcessed
{
    public Guid PaymentId { get; init; }
    public DateTime ProcessedOn { get; init; }
    public string Message { get; init; }
}
