namespace WeatherApi;

public record WeatherForecastRequestMessage
{
    public Guid Id { get; init; }
    public string Location { get; init; }
    public DateOnly RequestedDate { get; init; }
}
