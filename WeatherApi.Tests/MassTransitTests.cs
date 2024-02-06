using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
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
    private WebApplicationFactory<Program> _liveWebApplicationFactory;
    private WebApplicationFactory<Program> _inMemoryWebApplicationFactory;

    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:latest")
        //.WithPortBinding(1433, 1433)
        //.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .Build();

    private readonly ITestOutputHelper _testOutputHelper;

    public MassTransitTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _rabbitMqContainer.StartAsync();

        _liveWebApplicationFactory = new WebApplicationFactory<Program>()
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

        _inMemoryWebApplicationFactory = new WebApplicationFactory<Program>()
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

                    cfg.AddInMemoryInboxOutbox();

                    cfg.UsingInMemory((context, config) =>
                    {
                        config.ConfigureEndpoints(context);
                    });
                }));
            }); 

        var serviceScopeFactory = _liveWebApplicationFactory.Services.GetRequiredService<IServiceScopeFactory>();

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
    public async Task ShouldHaveSideEffects(string location)
    {
        var harness = _liveWebApplicationFactory.Services.GetTestHarness();

        using var client = _liveWebApplicationFactory.CreateClient();

        var response = await client.PostAsJsonAsync($"weatherforecast?location={location}", new {});

        var weatherGuid = await response.Content.ReadFromJsonAsync<Guid>();

        weatherGuid.Should().NotBe(Guid.Empty);

        //(await harness.Published.Any<WeatherForecastRequestMessage>(m => m.Context.Message.Id == weatherGuid)).Should().BeTrue();

        var act = async () =>
        {
            var consumerTestHarness = harness.GetConsumerHarness<WeatherForecastConsumer>();

            (await consumerTestHarness.Consumed.Any<WeatherForecastRequestMessage>(m =>
                m.Context.Message.Id == weatherGuid)).Should().BeTrue();

            var weatherForecast = await client.GetFromJsonAsync<WeatherForecast>($"weatherforecast/{weatherGuid}");

            weatherForecast!.Location.Should().Be(location);
            weatherForecast.Summary.Should().NotBeNull();
        };

        await act.Should().NotThrowAfterAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("Timbuktu")]
    [InlineData("Nowhere")]
    [InlineData("Somewhere")]
    [InlineData("Anywhere")]
    [InlineData("MiddleOfNowhere")]
    [InlineData("OnTheEdgeOfNowhere")]
    [InlineData("JustOutsideNowhere")]
    public async Task ShouldNotHaveSideEffects(string location)
    {
        var harness = _liveWebApplicationFactory.Services.GetTestHarness();

        await harness.Stop();

        using var client = _liveWebApplicationFactory.CreateClient();

        var response = await client.PostAsJsonAsync($"weatherforecast?location={location}", new { });

        var weatherGuid = await response.Content.ReadFromJsonAsync<Guid>();

        weatherGuid.Should().NotBe(Guid.Empty);

        //(await harness.Published.Any<WeatherForecastRequestMessage>(m => m.Context.Message.Id == weatherGuid)).Should().BeTrue();

        var act = async () =>
        {
            var consumerTestHarness = harness.GetConsumerHarness<WeatherForecastConsumer>();

            (await consumerTestHarness.Consumed.Any<WeatherForecastRequestMessage>(m =>
                m.Context.Message.Id == weatherGuid)).Should().BeFalse();

            var weatherForecast = await client.GetFromJsonAsync<WeatherForecast>($"weatherforecast/{weatherGuid}");

            weatherForecast.Location.Should().Be(location);
            weatherForecast.Summary.Should().BeNull();
        };

        await act.Should().NotThrowAfterAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("Timbuktu")]
    [InlineData("Nowhere")]
    [InlineData("Somewhere")]
    [InlineData("Anywhere")]
    [InlineData("MiddleOfNowhere")]
    [InlineData("OnTheEdgeOfNowhere")]
    [InlineData("JustOutsideNowhere")]
    public async Task ShouldHaveSideEffectsInMemory(string location)
    {
        var harness = _inMemoryWebApplicationFactory.Services.GetTestHarness();

        using var client = _inMemoryWebApplicationFactory.CreateClient();

        var response = await client.PostAsJsonAsync($"weatherforecast?location={location}", new { });

        var weatherGuid = await response.Content.ReadFromJsonAsync<Guid>();

        weatherGuid.Should().NotBe(Guid.Empty);

        //(await harness.Published.Any<WeatherForecastRequestMessage>(m => m.Context.Message.Id == weatherGuid)).Should().BeTrue();

        var act = async () =>
        {
            var consumerTestHarness = harness.GetConsumerHarness<WeatherForecastConsumer>();

            (await consumerTestHarness.Consumed.Any<WeatherForecastRequestMessage>(m =>
                m.Context.Message.Id == weatherGuid)).Should().BeTrue();

            var weatherForecast = await client.GetFromJsonAsync<WeatherForecast>($"weatherforecast/{weatherGuid}");

            weatherForecast.Location.Should().Be(location);
            weatherForecast.Summary.Should().NotBeNull();
        };

        await act.Should().NotThrowAfterAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("Timbuktu")]
    [InlineData("Nowhere")]
    [InlineData("Somewhere")]
    [InlineData("Anywhere")]
    [InlineData("MiddleOfNowhere")]
    [InlineData("OnTheEdgeOfNowhere")]
    [InlineData("JustOutsideNowhere")]
    public async Task ShouldNotHaveSideEffectsInMemory(string location)
    {
        var harness = _inMemoryWebApplicationFactory.Services.GetTestHarness();

        await harness.Stop();

        using var client = _inMemoryWebApplicationFactory.CreateClient();

        var response = await client.PostAsJsonAsync($"weatherforecast?location={location}", new { });

        var weatherGuid = await response.Content.ReadFromJsonAsync<Guid>();

        weatherGuid.Should().NotBe(Guid.Empty);

        //(await harness.Published.Any<WeatherForecastRequestMessage>(m => m.Context.Message.Id == weatherGuid)).Should().BeTrue();

        var act = async () =>
        {
            var consumerTestHarness = harness.GetConsumerHarness<WeatherForecastConsumer>();

            (await consumerTestHarness.Consumed.Any<WeatherForecastRequestMessage>(m =>
                m.Context.Message.Id == weatherGuid)).Should().BeFalse();

            var weatherForecast = await client.GetFromJsonAsync<WeatherForecast>($"weatherforecast/{weatherGuid}");

            weatherForecast.Location.Should().Be(location);
            weatherForecast.Summary.Should().BeNull();
        };

        await act.Should().NotThrowAfterAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();
        await _liveWebApplicationFactory.DisposeAsync();
        await _inMemoryWebApplicationFactory.DisposeAsync();
    }
}