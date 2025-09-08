var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("database");

var serviceBus = builder.AddAzureServiceBus("messaging");

builder.AddProject<Projects.PaymentApi>("paymentapi")
    .WithReference(serviceBus)
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
