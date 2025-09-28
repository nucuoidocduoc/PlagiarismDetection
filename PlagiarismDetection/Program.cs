using PlagiarismDetection.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();


// Register services
builder.Services.AddSingleton<CrawlerService>();
builder.Services.AddSingleton<TextProcessor>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<IVectorStore, QdrantVectorStore>();
builder.Services.AddSingleton<ReportService>();

var app = builder.Build();

/// Configuration (read from env or appsettings)

app.MapControllers();
app.Run();
