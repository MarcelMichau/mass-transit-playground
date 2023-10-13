using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Meziantou.Extensions.Logging.Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Xunit.Abstractions;

namespace WeatherApi.Tests;

public class MassTransitTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _webApplicationFactory;

    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:latest")
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("masstransit/rabbitmq").Build();

    private readonly ITestOutputHelper _testOutputHelper;

    public MassTransitTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _rabbitMqContainer.StartAsync();

        _webApplicationFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(lb =>
                {
                    lb.Services.AddSingleton<ILoggerProvider>(new XUnitLoggerProvider(_testOutputHelper, appendScope: false));
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

                    cfg.AddEntityFrameworkOutbox<WeatherDbContext>(o =>
                    {
                        o.UseSqlServer();
                        o.UseBusOutbox();
                    });

                    cfg.UsingRabbitMq((context, config) =>
                    {
                        config.ConfigureEndpoints(context);
                    });
                }));
            });

        var serviceScopeFactory = _webApplicationFactory.Services.GetRequiredService<IServiceScopeFactory>();

        using var scope = serviceScopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();

        await context.Database.MigrateAsync();
    }

    [Theory]
    [InlineData("Timbuktu")]
    [InlineData("Nowhere")]
    [InlineData("Somewhere")]
    [InlineData("Anywhere")]
    [InlineData("MiddleOfNowhere")]
    [InlineData("OnTheEdgeOfNowhere")]
    [InlineData("JustOutsideNowhere")]
    public async Task PublisherShouldPublish(string location)
    {
        using var client = _webApplicationFactory.CreateClient();

        var harness = _webApplicationFactory.Services.GetTestHarness();

        var response = await client.PostAsJsonAsync($"weatherforecast?location={location}", new {});

        var weatherGuid = await response.Content.ReadFromJsonAsync<Guid>();

        weatherGuid.Should().NotBe(Guid.Empty);

        (await harness.Published.Any<WeatherForecastRequestMessage>(m => m.Context.Message.Id == weatherGuid)).Should().BeTrue();

        var consumerTestHarness = harness.GetConsumerHarness<WeatherForecastConsumer>();

        (await consumerTestHarness.Consumed.Any<WeatherForecastRequestMessage>(m => m.Context.Message.Id == weatherGuid)).Should().BeTrue();

        var weatherForecast = await client.GetFromJsonAsync<WeatherForecast>($"weatherforecast/{weatherGuid}");

        weatherForecast.Location.Should().Be(location);
        weatherForecast.Summary.Should().NotBeNull();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();
        await _webApplicationFactory.DisposeAsync();
    }
}