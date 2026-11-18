using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace WeatherApi.Tests;

[Collection(IntegrationTestCollection.Name)]
public class MassTransitTests : IDisposable
{
    private readonly IntegrationTestFixture _fixture;

    public MassTransitTests(IntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.Output.Current = testOutputHelper;
    }

    public void Dispose() => _fixture.Output.Current = null;

    [Theory]
    [InlineData("Timbuktu")]
    [InlineData("Nowhere")]
    [InlineData("Somewhere")]
    [InlineData("Anywhere")]
    [InlineData("MiddleOfNowhere")]
    [InlineData("OnTheEdgeOfNowhere")]
    [InlineData("JustOutsideNowhere")]
    public Task ShouldHaveSideEffects(string location) =>
        AssertForecastIsProcessed(_fixture.RabbitMqWithConsumer, location);

    [Theory]
    [InlineData("Timbuktu")]
    [InlineData("Nowhere")]
    [InlineData("Somewhere")]
    [InlineData("Anywhere")]
    [InlineData("MiddleOfNowhere")]
    [InlineData("OnTheEdgeOfNowhere")]
    [InlineData("JustOutsideNowhere")]
    public Task ShouldNotHaveSideEffects(string location) =>
        AssertForecastIsNotProcessed(_fixture.RabbitMqWithoutConsumer, location);

    [Theory]
    [InlineData("Timbuktu")]
    [InlineData("Nowhere")]
    [InlineData("Somewhere")]
    [InlineData("Anywhere")]
    [InlineData("MiddleOfNowhere")]
    [InlineData("OnTheEdgeOfNowhere")]
    [InlineData("JustOutsideNowhere")]
    public Task ShouldHaveSideEffectsInMemory(string location) =>
        AssertForecastIsProcessed(_fixture.InMemoryWithConsumer, location);

    [Theory]
    [InlineData("Timbuktu")]
    [InlineData("Nowhere")]
    [InlineData("Somewhere")]
    [InlineData("Anywhere")]
    [InlineData("MiddleOfNowhere")]
    [InlineData("OnTheEdgeOfNowhere")]
    [InlineData("JustOutsideNowhere")]
    public Task ShouldNotHaveSideEffectsInMemory(string location) =>
        AssertForecastIsNotProcessed(_fixture.InMemoryWithoutConsumer, location);

    private static async Task AssertForecastIsProcessed(WebApplicationFactory<Program> factory, string location)
    {
        var harness = factory.Services.GetTestHarness();

        using var client = factory.CreateClient();

        var weatherGuid = await RequestForecast(client, location);

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

    // The host has no consumer registered, so the request is published but nothing processes it.
    private static async Task AssertForecastIsNotProcessed(WebApplicationFactory<Program> factory, string location)
    {
        var harness = factory.Services.GetTestHarness();

        using var client = factory.CreateClient();

        var weatherGuid = await RequestForecast(client, location);

        (await harness.Published.Any<WeatherForecastRequestMessage>(m =>
            m.Context.Message.Id == weatherGuid)).Should().BeTrue();

        (await harness.Consumed.Any<WeatherForecastRequestMessage>(m =>
            m.Context.Message.Id == weatherGuid)).Should().BeFalse();

        var weatherForecast = await client.GetFromJsonAsync<WeatherForecast>($"weatherforecast/{weatherGuid}");

        weatherForecast!.Location.Should().Be(location);
        weatherForecast.Summary.Should().BeNull();
    }

    private static async Task<Guid> RequestForecast(HttpClient client, string location)
    {
        var response = await client.PostAsJsonAsync($"weatherforecast?location={location}", new { });

        var weatherGuid = await response.Content.ReadFromJsonAsync<Guid>();

        weatherGuid.Should().NotBe(Guid.Empty);

        return weatherGuid;
    }
}
