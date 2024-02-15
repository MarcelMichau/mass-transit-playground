using MassTransit;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace PaymentApi.StateMachine;

public class PaymentStateMap : SagaClassMap<PaymentState>
{
    protected override void Configure(EntityTypeBuilder<PaymentState> entity, ModelBuilder model)
    {
        entity.Property(x => x.CurrentState);

        entity.Property(x => x.PaymentAmount);
        entity.Property(x => x.PaymentFromAccount);
        entity.Property(x => x.PaymentToAccount);

        entity.Property(x => x.Decision);
        entity.Property(x => x.DecisionReason);
    }
}
