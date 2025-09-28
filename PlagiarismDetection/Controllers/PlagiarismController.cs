using Microsoft.AspNetCore.Mvc;
using PlagiarismDetection.Models;
using PlagiarismDetection.Services;

namespace PlagiarismDetection.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlagiarismController : ControllerBase
    {
        private readonly CrawlerService _crawler;
        private readonly TextProcessor _processor;
        private readonly EmbeddingService _embedder;
        private readonly IVectorStore _vectorStore;
        private readonly ReportService _report;

        public PlagiarismController(CrawlerService crawler, TextProcessor processor, EmbeddingService embedder, IVectorStore vectorStore, ReportService report)
        {
            _crawler = crawler;
            _processor = processor;
            _embedder = embedder;
            _vectorStore = vectorStore;
            _report = report;
        }

        [HttpPost("ingest/arxiv")]
        public async Task<IActionResult> IngestArxiv([FromBody] IngestRequest request, [FromQuery] int max = 50)
        {
            var docs = await _crawler.FetchArxivAsync(request.Text, max);
            foreach (var d in docs)
            {
                var chunks = _processor.ChunkDocument(d);
                foreach (var c in chunks)
                {
                    // embed
                    var v = await _embedder.GetEmbeddingOpenAiAsync(c.Text);
                    var meta = new { DocumentId = d.Id, d.Title, d.Source };
                    await _vectorStore.UpsertAsync(c.Id, v, meta);
                }
            }
            return Ok(new { ingested = docs.Count() });
        }

        public class IngestRequest
        {
            public string Text { get; set; } = string.Empty;
        }

        [HttpPost("check")]
        public async Task<IActionResult> Check([FromBody] CheckRequest req)
        {
            // chunk input
            var doc = new Document { Id = Guid.NewGuid().ToString(), Title = req.Title ?? "(input)", Text = req.Text, Source = "uploaded" };
            var chunks = _processor.ChunkDocument(doc).ToList();
            var matchesAll = new List<dynamic>();
            foreach (var c in chunks)
            {
                var v = await _embedder.GetEmbeddingOpenAiAsync(c.Text);
                var hits = await _vectorStore.QueryAsync(v, topK: req.TopK ?? 5);
                foreach (var h in hits)
                {
                    matchesAll.Add(new { ChunkId = c.Id, h.Score, Source = (string)h.Metadata["Title"], Text = h.Metadata["payloadText"] ?? h.Metadata["text"] ?? "" });
                }
            }
            var html = _report.RenderHtml(doc.Text, matchesAll);
            return Content(html, "text/html");
        }
    }

    public class CheckRequest
    {
        public string? Title { get; set; }
        public string Text { get; set; } = string.Empty;
        public int? TopK { get; set; }
    }
}