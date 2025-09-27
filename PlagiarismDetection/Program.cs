using PlagiarismDetection.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var app = builder.Build();

/// Configuration (read from env or appsettings)
builder.Services.AddControllers();
builder.Services.AddHttpClient();


// Register services
builder.Services.AddSingleton<CrawlerService>();
builder.Services.AddSingleton<TextProcessor>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();
builder.Services.AddSingleton<ReportService>();

app.MapControllers();
app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
