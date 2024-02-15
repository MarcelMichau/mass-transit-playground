using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace WeatherApi;

public class WeatherForecastConsumer(ILogger<WeatherForecastConsumer> logger, WeatherDbContext context)
    : IConsumer<WeatherForecastRequestMessage>
{
    private readonly string[] _summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public async Task Consume(ConsumeContext<WeatherForecastRequestMessage> context1)
    {
        var forecast = new WeatherForecastResponseMessage
        (
            context1.Message.RequestedDate,
            Random.Shared.Next(-20, 55),
            _summaries[Random.Shared.Next(_summaries.Length)]
        );

        logger.LogInformation("Received WeatherForecastRequestMessage: {Location} {RequestedDate}", context1.Message.Location, context1.Message.RequestedDate);

        logger.LogInformation("Forecast for {Location} is: {Summary}", context1.Message.Location, forecast);

        var existingForecast = await context.WeatherForecasts.FirstOrDefaultAsync(f => f.Id == context1.Message.Id);

        if (existingForecast is not null)
        {
            existingForecast.TemperatureC = forecast.TemperatureC;
            existingForecast.TemperatureF = forecast.TemperatureF;
            existingForecast.Summary = forecast.Summary;

            await context.SaveChangesAsync();
        }
        else
        {
            logger.LogWarning("Forecast with Id {Id} not found", context1.Message.Id);
        }
    }
}
