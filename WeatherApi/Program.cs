using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using WeatherApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<WeatherDbContext>(optionsBuilder =>
{
    optionsBuilder.UseSqlServer(builder.Configuration.GetConnectionString(nameof(WeatherDbContext)));
});

builder.Services.AddMassTransit(configure =>
{
    var entryAssembly = Assembly.GetEntryAssembly();

    configure.AddConsumers(entryAssembly);

    configure.SetKebabCaseEndpointNameFormatter();

    configure.AddEntityFrameworkOutbox<WeatherDbContext>(o =>
    {
        // configure which database lock provider to use (Postgres, SqlServer, or MySql)
        o.UseSqlServer();

        // enable the bus outbox
        o.UseBusOutbox();
    });

    configure.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(new Uri("sb://sb-marcel-michau-test.servicebus.windows.net"));
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DisplayRequestDuration();
    });
}

app.UseHttpsRedirection();

app.MapPost("/weatherforecast",
        async (string location, WeatherDbContext context, IPublishEndpoint publishEndpoint) =>
        {
            var requestedDate = DateTime.Now;

            var newForecast = new WeatherForecast
            {
                Id = NewId.NextGuid(),
                Location = location,
                RequestedDate = requestedDate
            };

            context.WeatherForecasts.Add(newForecast);

            await publishEndpoint.Publish(new WeatherForecastRequestMessage
            {
                Id = newForecast.Id,
                Location = location,
                RequestedDate = DateOnly.FromDateTime(requestedDate)
            });

            await context.SaveChangesAsync();

            return Results.Accepted(value: newForecast.Id.ToString());
        })
    .WithName("GenerateWeatherForecast")
    .WithOpenApi();

app.MapGet("/weatherforecast/{id:guid}",
        async (Guid id, WeatherDbContext context) =>
        {
            var forecast = await context.WeatherForecasts.FirstOrDefaultAsync(f => f.Id == id);

            return forecast is null ? Results.NotFound() : Results.Ok(forecast);
        })
    .WithName("GetWeatherForecast")
    .WithOpenApi();

app.Run();