namespace PaymentApi.Domain;

public class Payment
{
    public Guid Id { get; init; }
    public DateTime CreatedOn { get; init; }
    public decimal Amount { get; init; }
    public string FromAccountNumber { get; init; }
    public string ToAccountNumber { get; init; }
}
