namespace PaymentApi;

public record PaymentApproved
{
    public Guid PaymentId { get; init; }
    public DateTime ApprovedOn { get; init; }
    public string Reason { get; init; }
}
