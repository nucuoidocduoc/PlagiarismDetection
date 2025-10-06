using Nest;
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
builder.Services.AddSingleton<SearchService>();

var elasticsearchUrl = "http://localhost:9200";
var settings = new ConnectionSettings(new Uri(elasticsearchUrl))
        .DefaultIndex("research-projects")
        .DisableDirectStreaming()
        .PrettyJson();

var client = new ElasticClient(settings);
builder.Services.AddSingleton(client);

var app = builder.Build();

/// Configuration (read from env or appsettings)

app.MapControllers();
app.Run();
