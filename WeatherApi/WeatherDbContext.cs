using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace WeatherApi;

public class WeatherDbContext : DbContext
{
    public WeatherDbContext(DbContextOptions<WeatherDbContext> options)
        : base(options)
    {
    }

    public DbSet<WeatherForecast> WeatherForecasts => Set<WeatherForecast>();

    //protected override void OnModelCreating(ModelBuilder modelBuilder)
    //{
    //    base.OnModelCreating(modelBuilder);

    //    modelBuilder.AddInboxStateEntity();
    //    modelBuilder.AddOutboxMessageEntity();
    //    modelBuilder.AddOutboxStateEntity();
    //}
}
