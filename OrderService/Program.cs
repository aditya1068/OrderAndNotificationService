
using Confluent.Kafka;
using OrderService.Application.Handlers;
using OrderService.Application.Services;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommandHandler).Assembly));

// Repositories
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

// Services
builder.Services.AddScoped<IOrderProcessingService, OrderProcessingService>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// HTTP Client for NotificationService
builder.Services.AddHttpClient<INotificationService, NotificationService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5000/api");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<IDatabase>(provider =>
    provider.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

// Kafka
builder.Services.AddSingleton<IProducer<string, string>>(provider =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration.GetConnectionString("Kafka") ?? "localhost:9092",
        Acks = Acks.Leader,
        MessageTimeoutMs = 10000,
    };
    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddScoped<IKafkaPublisher, KafkaPublisher>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
