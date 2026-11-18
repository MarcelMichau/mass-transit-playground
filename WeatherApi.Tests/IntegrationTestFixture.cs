using MassTransit;
using Meziantou.Extensions.Logging.Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace WeatherApi.Tests;

/// <summary>
/// Starts SQL Server and RabbitMQ once for every test in <see cref="IntegrationTestCollection"/>,
/// then starts one WeatherApi host per scenario. The hosts and their MassTransit test harnesses
/// stay running for the whole test run; tests never stop or restart them.
/// </summary>
/// <remarks>
/// Each host gets its own database and RabbitMQ virtual host inside the shared containers, so a
/// message published by the host without a consumer can't be picked up by the host with one.
/// </remarks>
public sealed class IntegrationTestFixture : IAsyncLifetime
{
    // Pinned to SQL Server 2022, as Aspire and fake-survey-generator use. The 2025 image (:latest)
    // intermittently fails to start under load with "Failed to load LSA: 0xc0070102" (a wait timeout).
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .Build();

    private readonly List<WebApplicationFactory<Program>> _factories = [];

    /// <summary>Forwards host logs to the output of whichever test is currently running.</summary>
    public TestOutputForwarder Output { get; } = new();

    public WebApplicationFactory<Program> RabbitMqWithConsumer { get; private set; } = null!;
    public WebApplicationFactory<Program> RabbitMqWithoutConsumer { get; private set; } = null!;
    public WebApplicationFactory<Program> InMemoryWithConsumer { get; private set; } = null!;
    public WebApplicationFactory<Program> InMemoryWithoutConsumer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_dbContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        RabbitMqWithConsumer = await StartHostAsync("rabbitmq-with-consumer", useRabbitMq: true, withConsumer: true);
        RabbitMqWithoutConsumer = await StartHostAsync("rabbitmq-without-consumer", useRabbitMq: true, withConsumer: false);
        InMemoryWithConsumer = await StartHostAsync("in-memory-with-consumer", useRabbitMq: false, withConsumer: true);
        InMemoryWithoutConsumer = await StartHostAsync("in-memory-without-consumer", useRabbitMq: false, withConsumer: false);
    }

    public async Task DisposeAsync()
    {
        foreach (var factory in _factories)
        {
            try
            {
                await factory.DisposeAsync();
            }
            catch (NullReferenceException)
            {
                // MassTransit 9.2.3's BusDepotAgentSupervisor.PreStop checks its supervisor field for null
                // and then reads it again, so two overlapping stops of the same bus (registered agents
                // such as the outbox delivery service make this possible) can throw here. It only
                // happens during shutdown, after every test has finished, so carry on cleaning up.
            }
        }

        await _dbContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();
    }

    private async Task<WebApplicationFactory<Program>> StartHostAsync(string name, bool useRabbitMq, bool withConsumer)
    {
        var databaseConnectionString = new SqlConnectionStringBuilder(_dbContainer.GetConnectionString())
        {
            InitialCatalog = name
        }.ConnectionString;

        // Migrate before the host starts, so its outbox delivery service finds its tables.
        var dbOptions = new DbContextOptionsBuilder<WeatherDbContext>()
            .UseSqlServer(databaseConnectionString)
            .Options;

        await using (var context = new WeatherDbContext(dbOptions))
            await context.Database.MigrateAsync();

        var rabbitMqHost = useRabbitMq ? await CreateVirtualHostAsync(name) : null;

        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(lb =>
                {
                    lb.Services.AddSingleton<ILoggerProvider>(new XUnitLoggerProvider(Output, appendScope: false));
                });

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<DbContextOptions<WeatherDbContext>>();
                    services.AddDbContext<WeatherDbContext>(optionsBuilder =>
                    {
                        optionsBuilder.UseSqlServer(databaseConnectionString);
                    });
                });

                builder.ConfigureServices(services => services.AddMassTransitTestHarness(cfg =>
                {
                    // The harness lives for the whole run, so don't let its default timeout expire mid-run.
                    cfg.SetTestTimeouts(testTimeout: TimeSpan.FromMinutes(10));

                    if (withConsumer)
                        cfg.AddConsumer<WeatherForecastConsumer>();

                    // As in Program.cs: the POST endpoint publishes before it saves, so only the bus
                    // outbox stops the consumer from looking for a forecast that isn't saved yet.
                    cfg.AddEntityFrameworkOutbox<WeatherDbContext>(o =>
                    {
                        o.UseSqlServer();
                        o.UseBusOutbox();
                    });

                    if (rabbitMqHost is not null)
                    {
                        cfg.UsingRabbitMq((context, config) =>
                        {
                            config.Host(rabbitMqHost);

                            config.ConfigureEndpoints(context);
                        });
                    }
                    else
                    {
                        cfg.UsingInMemory((context, config) =>
                        {
                            config.ConfigureEndpoints(context);
                        });
                    }
                }));
            });

        _factories.Add(factory);

        // Resolving the services builds and starts the host, which starts the bus.
        _ = factory.Services;

        return factory;
    }

    private async Task<Uri> CreateVirtualHostAsync(string name)
    {
        await ExecRabbitMqCtlAsync("add_vhost", name);
        await ExecRabbitMqCtlAsync("set_permissions", "-p", name, RabbitMqBuilder.DefaultUsername, ".*", ".*", ".*");

        return new Uri($"{_rabbitMqContainer.GetConnectionString().TrimEnd('/')}/{name}");
    }

    private async Task ExecRabbitMqCtlAsync(params string[] args)
    {
        var result = await _rabbitMqContainer.ExecAsync(["rabbitmqctl", .. args]);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"rabbitmqctl {string.Join(' ', args)} failed: {result.Stderr}");
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "Integration tests";
}
