using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Meziantou.Extensions.Logging.Xunit;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace WeatherApi.Tests;
public class IntegrationTestFixture : IAsyncLifetime
{
    public WebApplicationFactory<Program> Factory;

    private readonly MsSqlContainer _dbContainer =
        new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:latest")
            .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        //.WithPortBinding(5672, 5672)
        //.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
        .Build();

    public ITestOutputHelper TestOutputHelper { get; set; }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _rabbitMqContainer.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(lb =>
                {
                    lb.ClearProviders();
                    lb.Services.AddSingleton<ILoggerProvider>(new XUnitLoggerProvider(TestOutputHelper, appendScope: false));
                });

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<DbContextOptions<WeatherDbContext>>();
                    services.AddDbContext<WeatherDbContext>(optionsBuilder =>
                    {
                        optionsBuilder.UseSqlServer(_dbContainer.GetConnectionString());
                    });
                });

                builder.ConfigureServices(services => services.AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddConsumer<WeatherForecastConsumer>();

                    //cfg.AddEntityFrameworkOutbox<WeatherDbContext>(o =>
                    //{
                    //    o.UseSqlServer();
                    //    o.UseBusOutbox();
                    //});

                    cfg.UsingRabbitMq((context, config) =>
                    {
                        config.Host(_rabbitMqContainer.GetConnectionString());

                        config.ConfigureEndpoints(context);
                    });
                }));
            });

        var serviceScopeFactory = Factory.Services.GetRequiredService<IServiceScopeFactory>();

        using var scope = serviceScopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();

        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();

        await Factory.DisposeAsync();
    }
}
