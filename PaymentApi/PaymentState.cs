using MassTransit;

namespace PaymentApi;

public class PaymentState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public decimal PaymentAmount { get; set; }
    public string PaymentFromAccount { get; set; }
    public string PaymentToAccount { get; set; }

    public string? Decision { get; set; }
    public string? DecisionReason { get; set; }

    public string CurrentState { get; set; } = null!;
}
