namespace PaymentApi.Messages;

public record SubmitPayment
{
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
    public string FromAccountNumber { get; init; }
    public string ToAccountNumber { get; init; }
    public string DecisionReason { get; init; }
}
