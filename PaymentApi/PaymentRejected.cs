namespace PaymentApi;

public record PaymentRejected
{
    public Guid PaymentId { get; init; }
    public DateTime RejectedOn { get; init; }
    public string Reason { get; init; }
}
