using Play.Common.MassTransit;
using Play.Common.MongoDb;
using Play.Common.Settings;
using Play.Inventory.Service.Clients;
using Play.Inventory.Service.Entities;
using Polly;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var serviceSettings = builder.Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
builder.Services.AddMongo();
builder.Services.AddMongoRepository<InventoryItem>("inventoryitems");
builder.Services.AddMongoRepository<CatalogItem>("catalogitems");
builder.Services.AddMassTransitWithRabbitMq();

//AddCatalogClient(builder);

builder.Services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void AddCatalogClient(WebApplicationBuilder builder)
{
    Random jitterer = new Random();

    builder.Services.AddHttpClient<CatalogClient>(client =>
    {
        client.BaseAddress = new Uri("https://localhost:7159");
    })
        .AddTransientHttpErrorPolicy(builder2 => builder2.Or<TimeoutRejectedException>().WaitAndRetryAsync(
            5,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(jitterer.Next(0, 1000))//,
                                                                                                                               //onRetry: (outcome, timespan, retryAttempt) =>
                                                                                                                               //{
                                                                                                                               //    // don't do in prod
                                                                                                                               //    var serviceProvider = builder.Services.BuildServiceProvider();
                                                                                                                               //    serviceProvider.GetService<ILogger<CatalogClient>>().LogWarning($"Delaying for {timespan.TotalSeconds} seconds, then making retry {retryAttempt}");
                                                                                                                               //}
        ))
        .AddTransientHttpErrorPolicy(builder2 => builder2.Or<TimeoutRejectedException>().CircuitBreakerAsync(
            3,
            TimeSpan.FromSeconds(15)//,
                                    //onBreak: (outcome, timespan) =>
                                    //{
                                    //    // don't do in prod
                                    //    var serviceProvider = builder.Services.BuildServiceProvider();
                                    //    serviceProvider.GetService<ILogger<CatalogClient>>().LogWarning($"Opening circuit for {timespan.TotalSeconds} seconds...");
                                    //},
                                    //onReset: () =>
                                    //{
                                    //    // don't do in prod
                                    //    var serviceProvider = builder.Services.BuildServiceProvider();
                                    //    serviceProvider.GetService<ILogger<CatalogClient>>().LogWarning($"Closing the circuit.");
                                    //}
        ))
        .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(1));
}