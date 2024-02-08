﻿using MassTransit;

namespace PaymentApi;

public class PaymentStateMachine : MassTransitStateMachine<PaymentState>
{
    public PaymentStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => PaymentCreated, x => x.CorrelateById(m => m.Message.PaymentId));
        Event(() => PaymentApproved, x => x.CorrelateById(m => m.Message.PaymentId));
        Event(() => PaymentRejected, x => x.CorrelateById(m => m.Message.PaymentId));
        Event(() => PaymentSubmitted, x => x.CorrelateById(m => m.Message.PaymentId));

        Initially(
            When(PaymentCreated)
                .Then(context =>
                {
                    context.Saga.PaymentAmount = context.Message.Amount;
                    context.Saga.PaymentFromAccount = context.Message.FromAccountNumber;
                    context.Saga.PaymentToAccount = context.Message.ToAccountNumber;
                })
                .TransitionTo(AwaitingApproval)
                .Send(context => new SendPaymentApprovalNotification
                {
                    Amount = context.Saga.PaymentAmount,
                    PaymentId = context.Saga.CorrelationId
                }));

        During(AwaitingApproval,
            When(PaymentApproved)
                .Then(context =>
                {
                    context.Saga.Decision = context.Saga.PaymentAmount > 1000 ? "First Line Approved" : "Approved";
                    context.Saga.DecisionReason = context.Message.Reason;
                })
                .IfElse(context => context.Saga.PaymentAmount > 1000, aboveSingleApprovalThreshold =>
                        aboveSingleApprovalThreshold.TransitionTo(AwaitingSecondLineApproval),
                    belowSingleApprovalThreshold =>
                        belowSingleApprovalThreshold.TransitionTo(Approved)
                            .Send(context => new SubmitPayment
                            {
                                DecisionReason = context.Saga.DecisionReason,
                                Amount = context.Saga.PaymentAmount,
                                PaymentId = context.Saga.CorrelationId,
                                FromAccountNumber = context.Saga.PaymentFromAccount,
                                ToAccountNumber = context.Saga.PaymentToAccount
                            })));

        During(AwaitingSecondLineApproval,
            When(PaymentApproved)
                .Then(context =>
                {
                    context.Saga.Decision = "Second Line Approved";
                    context.Saga.DecisionReason = context.Message.Reason;
                })
                .TransitionTo(Approved)
                .Send(context => new SubmitPayment
                {
                    DecisionReason = context.Saga.DecisionReason,
                    Amount = context.Saga.PaymentAmount,
                    PaymentId = context.Saga.CorrelationId,
                    FromAccountNumber = context.Saga.PaymentFromAccount,
                    ToAccountNumber = context.Saga.PaymentToAccount
                }));

        During(AwaitingApproval,
            When(PaymentRejected)
                .Then(context =>
                {
                    context.Saga.Decision = "Rejected";
                    context.Saga.DecisionReason = context.Message.Reason;
                })
                .TransitionTo(Rejected));

        During(Approved,
            When(PaymentSubmitted)
                .TransitionTo(Completed));
    }

    public Event<PaymentCreated> PaymentCreated { get; } = null!;
    public Event<PaymentApproved> PaymentApproved { get; } = null!;
    public Event<PaymentRejected> PaymentRejected { get; } = null!;
    public Event<PaymentSubmitted> PaymentSubmitted { get; } = null!;

    public State AwaitingApproval { get; } = null!;
    public State AwaitingSecondLineApproval { get; } = null!;
    public State Approved { get; } = null!;
    public State Rejected { get; } = null!;
    public State Completed { get; } = null!;
}