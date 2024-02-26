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

    //configure.UsingInMemory((context, cfg) =>
    //{
    //    cfg.ConfigureEndpoints(context);
    //});

    // Uncomment below when testing locally with Azure Service Bus transport

    configure.AddServiceBusMessageScheduler();

    configure.AddConfigureEndpointsCallback((_, cfg) =>
    {
        if (cfg is not IServiceBusReceiveEndpointConfigurator sb) return;
        sb.ConfigureDeadLetterQueueDeadLetterTransport();
        sb.ConfigureDeadLetterQueueErrorTransport();
    });

    configure.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(new Uri("sb://someservicebus.servicebus.windows.net"));
        cfg.UseServiceBusMessageScheduler();
        cfg.ConfigureEndpoints(context);
    });

    configure.AddRider(rider =>
    {
        rider.UsingEventHub((context, cfg) =>
        {
            cfg.Host("someeventhub.servicebus.windows.net", new DefaultAzureCredential());

            cfg.Storage(new Uri("https://somestorageaccount.blob.core.windows.net/masstransit"), new DefaultAzureCredential());
        });
    });
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
