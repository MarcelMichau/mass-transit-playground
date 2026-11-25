var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("database");

var serviceBus = builder.AddAzureServiceBus("messaging");

builder.AddDotnetProject("paymentapi", "../PaymentApi/PaymentApi.csproj")
    .WithReference(serviceBus)
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
