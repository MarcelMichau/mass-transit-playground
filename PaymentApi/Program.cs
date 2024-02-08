using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

    configure.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
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
