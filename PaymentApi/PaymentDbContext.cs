using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace PaymentApi;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : SagaDbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new PaymentStateMap(); }
    }
}
