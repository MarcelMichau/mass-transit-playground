using MassTransit;
using PaymentApi.Events;
using PaymentApi.Messages;
using PaymentApi.Persistence;

namespace PaymentApi.StateMachine;

public class PaymentStateMachine : MassTransitStateMachine<PaymentState>
{
    public PaymentStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => PaymentCreated, x => x.CorrelateById(m => m.Message.PaymentId));
        Event(() => PaymentApproved, x => x.CorrelateById(m => m.Message.PaymentId));
        Event(() => PaymentRejected, x => x.CorrelateById(m => m.Message.PaymentId));
        Event(() => PaymentSubmitted, x => x.CorrelateById(m => m.Message.PaymentId));
        Event(() => PaymentProcessed, x => x.CorrelateById(m => m.Message.PaymentId));

        // Define the schedule for payment expiration
        Schedule(() => PaymentExpirationSchedule, x => x.ExpirationTokenId, s =>
        {
            s.Delay = TimeSpan.FromMinutes(5);
            s.Received = x => x.CorrelateById(context => context.Message.PaymentId);
        });

        Initially(
            When(PaymentCreated)
                .Then(context =>
                {
                    context.Saga.PaymentAmount = context.Message.Amount;
                    context.Saga.PaymentFromAccount = context.Message.FromAccountNumber;
                    context.Saga.PaymentToAccount = context.Message.ToAccountNumber;
                    context.Saga.CreatedOn = context.Message.CreatedOn;
                })
                .IfElse(context => context.Message.ExpirationTime != DateTimeOffset.MinValue,
                    // Schedule expiration only if ExpirationTime is not MinValue
                    hasExpiration => hasExpiration
                        .Schedule(PaymentExpirationSchedule, 
                            context => new PaymentExpirationRequested { PaymentId = context.Saga.CorrelationId },
                            context => context.Message.ExpirationTime.LocalDateTime),

                    // Skip scheduling if ExpirationTime is MinValue
                    noExpiration => noExpiration
                )
                .TransitionTo(AwaitingApproval)
                .Publish(context => new SendPaymentApprovalNotification
                {
                    Amount = context.Saga.PaymentAmount,
                    PaymentId = context.Saga.CorrelationId
                }));

        // Add handlers for payment expiration in each relevant state
        During(AwaitingApproval,
            When(PaymentExpirationSchedule.Received)
                .Then(context =>
                {
                    context.Saga.Decision = "Expired";
                    context.Saga.DecisionReason = "Payment expired after 5 minutes";
                })
                .TransitionTo(Expired)
                .Publish(context => new PaymentExpired
                {
                    PaymentId = context.Saga.CorrelationId,
                    ExpiredOn = DateTime.UtcNow
                }));

        During(AwaitingSecondLineApproval,
            When(PaymentExpirationSchedule.Received)
                .Then(context =>
                {
                    context.Saga.Decision = "Expired";
                    context.Saga.DecisionReason = "Payment expired after 5 minutes";
                })
                .TransitionTo(Expired)
                .Publish(context => new PaymentExpired
                {
                    PaymentId = context.Saga.CorrelationId,
                    ExpiredOn = DateTime.UtcNow
                }));

        // Existing behavior for AwaitingApproval
        During(AwaitingApproval,
            When(PaymentApproved)
                .Unschedule(PaymentExpirationSchedule)
                .Then(context =>
                {
                    context.Saga.Decision = context.Saga.PaymentAmount > 1000 ? "First Line Approved" : "Approved";
                    context.Saga.DecisionReason = context.Message.Reason;
                })
                .IfElse(context => context.Saga.PaymentAmount > 1000, aboveSingleApprovalThreshold =>
                        aboveSingleApprovalThreshold.TransitionTo(AwaitingSecondLineApproval),
                    belowSingleApprovalThreshold =>
                        belowSingleApprovalThreshold.TransitionTo(Approved)
                            .Publish(context => new SubmitPayment
                            {
                                DecisionReason = context.Saga.DecisionReason,
                                Amount = context.Saga.PaymentAmount,
                                PaymentId = context.Saga.CorrelationId,
                                FromAccountNumber = context.Saga.PaymentFromAccount,
                                ToAccountNumber = context.Saga.PaymentToAccount
                            })));

        During(AwaitingSecondLineApproval,
            When(PaymentApproved)
                .Unschedule(PaymentExpirationSchedule)
                .Then(context =>
                {
                    context.Saga.Decision = "Second Line Approved";
                    context.Saga.DecisionReason = context.Message.Reason;
                })
                .TransitionTo(Approved)
                .Publish(context => new SubmitPayment
                {
                    DecisionReason = context.Saga.DecisionReason,
                    Amount = context.Saga.PaymentAmount,
                    PaymentId = context.Saga.CorrelationId,
                    FromAccountNumber = context.Saga.PaymentFromAccount,
                    ToAccountNumber = context.Saga.PaymentToAccount
                }));

        During(AwaitingApproval,
            When(PaymentRejected)
                .Unschedule(PaymentExpirationSchedule)
                .Then(context =>
                {
                    context.Saga.Decision = "Rejected";
                    context.Saga.DecisionReason = context.Message.Reason;
                })
                .TransitionTo(Rejected));

        During(Approved,
            When(PaymentSubmitted)
                .TransitionTo(AwaitingProcessingConfirmation)
                .Publish(context => new CheckPaymentStatus
                {
                    PaymentId = context.Message.PaymentId
                }));

        During(AwaitingProcessingConfirmation,
            When(PaymentProcessed)
                .TransitionTo(Completed));
    }

    public Event<PaymentCreated> PaymentCreated { get; init; } = null!;
    public Event<PaymentApproved> PaymentApproved { get; init; } = null!;
    public Event<PaymentRejected> PaymentRejected { get; init; } = null!;
    public Event<PaymentSubmitted> PaymentSubmitted { get; init; } = null!;
    public Event<PaymentProcessed> PaymentProcessed { get; init; } = null!;

    public Schedule<PaymentState, PaymentExpirationRequested> PaymentExpirationSchedule { get; init; } = null!;

    public State AwaitingApproval { get; init; } = null!;
    public State AwaitingSecondLineApproval { get; init; } = null!;
    public State Approved { get; init; } = null!;
    public State Rejected { get; init; } = null!;
    public State AwaitingProcessingConfirmation { get; init; } = null!;
    public State Completed { get; init; } = null!;
    public State Expired { get; init; } = null!;
}

public class PaymentStateMachineDefinition : SagaDefinition<PaymentState>
{
    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator, ISagaConfigurator<PaymentState> sagaConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        endpointConfigurator.UseEntityFrameworkOutbox<PaymentDbContext>(context);
    }
}