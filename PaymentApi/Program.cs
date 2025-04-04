using System.Reflection;
using Azure.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentApi.Persistence;
using PaymentApi.StateMachine;
using PaymentApi.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//builder.Services.AddHostedService<PaymentProviderPollingWorker>();

builder.Services.AddDbContext<PaymentDbContext>(optionsBuilder =>
{
    optionsBuilder.UseSqlServer(builder.Configuration.GetConnectionString(nameof(PaymentDbContext)));
});

builder.Services.AddMassTransit(configure =>
{
    var entryAssembly = Assembly.GetEntryAssembly();

    configure.AddConsumers(entryAssembly);

    configure.AddSagaStateMachine<PaymentStateMachine, PaymentState>()
        .EntityFrameworkRepository(r =>
        {
            r.ExistingDbContext<PaymentDbContext>();
            r.UseSqlServer();
        });

    configure.SetKebabCaseEndpointNameFormatter();
    configure.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    // Uncomment below when testing locally with in-memory transport

    configure.AddDelayedMessageScheduler();

    configure.UsingInMemory((context, cfg) =>
    {
        cfg.UseDelayedMessageScheduler();
        cfg.ConfigureEndpoints(context);
    });

    // Uncomment below when testing locally with Azure Service Bus transport

    //configure.AddServiceBusMessageScheduler();

    //configure.AddConfigureEndpointsCallback((_, cfg) =>
    //{
    //    if (cfg is not IServiceBusReceiveEndpointConfigurator sb) return;
    //    sb.ConfigureDeadLetterQueueDeadLetterTransport();
    //    sb.ConfigureDeadLetterQueueErrorTransport();
    //});

    //configure.UsingAzureServiceBus((context, cfg) =>
    //{
    //    cfg.Host(new Uri(builder.Configuration.GetValue<string>("AZURE_SERVICE_BUS_NAMESPACE")));
    //    cfg.UseServiceBusMessageScheduler();
    //    cfg.ConfigureEndpoints(context);
    //});

    // Uncomment below when using Azure Event Hub transport
    //configure.AddRider(rider =>
    //{
    //    rider.UsingEventHub((context, cfg) =>
    //    {
    //        cfg.Host(builder.Configuration.GetValue<string>("AZURE_EVENT_HUB_NAMESPACE"), new DefaultAzureCredential());

    //        cfg.Storage(new Uri(builder.Configuration.GetValue<string>("AZURE_STORAGE_ACCOUNT_CONTAINER_URI")), new DefaultAzureCredential());
    //    });
    //});
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
