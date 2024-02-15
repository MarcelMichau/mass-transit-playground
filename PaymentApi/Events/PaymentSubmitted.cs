namespace PaymentApi.Events;

public record PaymentSubmitted
{
    public Guid PaymentId { get; init; }
    public DateTime SubmittedOn { get; init; }
    public string Reference { get; init; }
}
