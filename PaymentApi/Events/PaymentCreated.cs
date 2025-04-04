namespace PaymentApi.Events;

public record PaymentCreated
{
    public Guid PaymentId { get; init; }
    public DateTime CreatedOn { get; init; }
    public decimal Amount { get; init; }
    public string FromAccountNumber { get; init; }
    public string ToAccountNumber { get; init; }
    public TimeSpan ExpirationTime { get; init; }
}
