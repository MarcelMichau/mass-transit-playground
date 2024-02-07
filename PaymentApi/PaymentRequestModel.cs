namespace PaymentApi;

public class PaymentRequestModel
{
    public decimal Amount { get; init; }
    public string FromAccountNumber { get; init; }
    public string ToAccountNumber { get; init; }
}
