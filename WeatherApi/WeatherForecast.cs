namespace WeatherApi;

public class WeatherForecast
{
    public Guid Id { get; set; }
    public DateTime RequestedDate { get; set; }
    public string Location { get; set; }
    public int TemperatureC { get; set; }
    public int TemperatureF { get; set; }
    public string? Summary { get; set; }
}