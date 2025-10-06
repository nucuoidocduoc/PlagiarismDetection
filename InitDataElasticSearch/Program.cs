using InitDataElasticSearch;
using Nest;
using System.Text.Json;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Cấu hình
var elasticsearchUrl = "http://localhost:9200"; // Thay đổi URL của bạn
var indexName = "research-projects"; // Tên index
var jsonFilePath = "./scientific_projects_full.json"; // Đường dẫn file JSON
var batchSize = 500; // Số lượng documents mỗi batch

Console.WriteLine("=== Elasticsearch Indexer ===");
Console.WriteLine($"Elasticsearch URL: {elasticsearchUrl}");
Console.WriteLine($"Index Name: {indexName}");
Console.WriteLine($"File Path: {jsonFilePath}");
Console.WriteLine();

try
{
    // Kiểm tra file tồn tại
    if (!File.Exists(jsonFilePath))
    {
        Console.WriteLine($"❌ File không tồn tại: {jsonFilePath}");
        return;
    }

    // Đọc và parse JSON
    Console.WriteLine("📖 Đang đọc file JSON...");
    var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
    var projects = JsonSerializer.Deserialize<List<Project>>(jsonContent, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    if (projects == null || projects.Count == 0)
    {
        Console.WriteLine("❌ Không có dữ liệu trong file JSON");
        return;
    }

    Console.WriteLine($"✅ Đọc thành công {projects.Count} records");
    Console.WriteLine();

    // Kết nối Elasticsearch
    Console.WriteLine("🔌 Đang kết nối Elasticsearch...");
    var settings = new ConnectionSettings(new Uri(elasticsearchUrl))
        .DefaultIndex(indexName)
        .DisableDirectStreaming()
        .PrettyJson();

    var client = new ElasticClient(settings);

    // Kiểm tra kết nối
    var pingResponse = await client.PingAsync();
    if (!pingResponse.IsValid)
    {
        Console.WriteLine($"❌ Không thể kết nối Elasticsearch: {pingResponse.DebugInformation}");
        return;
    }
    Console.WriteLine("✅ Kết nối Elasticsearch thành công");
    Console.WriteLine();

    // Kiểm tra và tạo index nếu chưa tồn tại
    var indexExistsResponse = await client.Indices.ExistsAsync(indexName);
    if (!indexExistsResponse.Exists)
    {
        Console.WriteLine($"📝 Đang tạo index '{indexName}'...");
        var createIndexResponse = await client.Indices.CreateAsync(indexName, c => c
            .Map<Project>(m => m
                .AutoMap()
                .Properties(p => p
                    .Text(t => t.Name(n => n.Title).Analyzer("standard"))
                    .Text(t => t.Name(n => n.Abstract).Analyzer("standard"))
                    .Text(t => t.Name(n => n.Keywords).Analyzer("standard"))
                    .Keyword(k => k.Name(n => n.ProjectCode))
                    .Keyword(k => k.Name(n => n.Status))
                )
            )
        );

        if (!createIndexResponse.IsValid)
        {
            Console.WriteLine($"❌ Không thể tạo index: {createIndexResponse.DebugInformation}");
            return;
        }
        Console.WriteLine("✅ Tạo index thành công");
    }
    else
    {
        Console.WriteLine($"ℹ️  Index '{indexName}' đã tồn tại");
    }
    Console.WriteLine();

    // Index documents theo batch
    Console.WriteLine($"📤 Đang index {projects.Count} documents (batch size: {batchSize})...");
    var totalIndexed = 0;
    var batches = projects.Select((item, index) => new { item, index })
                        .GroupBy(x => x.index / batchSize)
                        .Select(g => g.Select(x => x.item).ToList())
                        .ToList();

    for (int i = 0; i < batches.Count; i++)
    {
        var batch = batches[i];
        var bulkResponse = await client.BulkAsync(b => b
            .Index(indexName)
            .IndexMany(batch, (bd, doc) => bd
                .Id(Guid.NewGuid())
                .Document(doc)
            )
        );

        if (bulkResponse.Errors)
        {
            Console.WriteLine($"⚠️  Lỗi khi index batch {i + 1}: {bulkResponse.DebugInformation}");
            foreach (var item in bulkResponse.ItemsWithErrors)
            {
                Console.WriteLine($"   - Document ID {item.Id}: {item.Error.Reason}");
            }
        }
        else
        {
            totalIndexed += batch.Count;
            Console.WriteLine($"✅ Batch {i + 1}/{batches.Count}: Indexed {batch.Count} documents (Tổng: {totalIndexed}/{projects.Count})");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"🎉 Hoàn thành! Đã index {totalIndexed}/{projects.Count} documents");

    // Refresh index
    await client.Indices.RefreshAsync(indexName);

    // Kiểm tra số lượng documents
    var countResponse = await client.CountAsync<Project>(c => c.Index(indexName));
    Console.WriteLine($"📊 Tổng số documents trong index: {countResponse.Count}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Lỗi: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

Console.WriteLine();
Console.WriteLine("Nhấn phím bất kỳ để thoát...");
Console.ReadKey();