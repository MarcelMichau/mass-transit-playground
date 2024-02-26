using MassTransit;
using PaymentApi.Messages;

namespace PaymentApi.Consumers;

public class ThirdPartyMessageConsumer(ILogger<ThirdPartyMessageConsumer> logger) : IConsumer<ThirdPartyThing>
{
    public Task Consume(ConsumeContext<ThirdPartyThing> context)
    {
        logger.LogInformation("Received third party message: {SomeInformation}", context.Message.SomeInformation);

        return Task.CompletedTask;
    }
}

public class ThirdPartyMessageConsumerDefinition : ConsumerDefinition<ThirdPartyMessageConsumer>
{
    public ThirdPartyMessageConsumerDefinition()
    {
        EndpointName = "third-party-queue";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<ThirdPartyMessageConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.ConfigureConsumeTopology = false;
        endpointConfigurator.UseRawJsonDeserializer();
    }
}

