using MassTransit;

namespace PaymentApi.StateMachine;

public class PaymentState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public decimal PaymentAmount { get; set; }
    public string PaymentFromAccount { get; set; }
    public string PaymentToAccount { get; set; }

    public string? Decision { get; set; }
    public string? DecisionReason { get; set; }
    
    // Added to track payment creation time for expiration
    public DateTime CreatedOn { get; set; }

    // To store scheduler token for timeout
    public Guid? ExpirationTokenId { get; set; }

    public string CurrentState { get; set; } = null!;
}
