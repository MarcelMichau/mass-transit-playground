using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace WeatherApi;

public class WeatherForecastConsumer : IConsumer<WeatherForecastRequestMessage>
{
    private readonly string[] _summaries =
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastConsumer> _logger;
    private readonly WeatherDbContext _context;

    public WeatherForecastConsumer(ILogger<WeatherForecastConsumer> logger, WeatherDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task Consume(ConsumeContext<WeatherForecastRequestMessage> context)
    {
        var forecast = new WeatherForecastResponseMessage
        (
            context.Message.RequestedDate,
            Random.Shared.Next(-20, 55),
            _summaries[Random.Shared.Next(_summaries.Length)]
        );

        _logger.LogInformation("Received WeatherForecastRequestMessage: {Location} {RequestedDate}", context.Message.Location, context.Message.RequestedDate);

        _logger.LogInformation("Forecast for {Location} is: {Summary}", context.Message.Location, forecast);

        var existingForecast = await _context.WeatherForecasts.FirstOrDefaultAsync(f => f.Id == context.Message.Id);

        if (existingForecast is not null)
        {
            existingForecast.TemperatureC = forecast.TemperatureC;
            existingForecast.TemperatureF = forecast.TemperatureF;
            existingForecast.Summary = forecast.Summary;

            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogWarning("Forecast with Id {Id} not found", context.Message.Id);
        }
    }
}
