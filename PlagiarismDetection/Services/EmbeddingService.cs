using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace PlagiarismDetection.Services
{
    public class EmbeddingService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly string? _openAiKey;

        public EmbeddingService(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _openAiKey = config["OPENAI_API_KEY"]; // optional
        }

        // Option A: Call OpenAI Embeddings API (if key provided)
        public async Task<float[]> GetEmbeddingOpenAiAsync(string text)
        {
            if (string.IsNullOrEmpty(_openAiKey)) throw new InvalidOperationException("OPENAI_API_KEY not set.");
            var client = _httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiKey);
            var payload = new { input = text, model = "text-embedding-3-small" };
            var resp = await client.PostAsync("https://api.openai.com/v1/embeddings", new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();
            var j = JsonConvert.DeserializeObject<dynamic>(await resp.Content.ReadAsStringAsync());
            var vec = ((IEnumerable<object>)j.data[0].embedding).Select(x => Convert.ToSingle(x)).ToArray();
            return vec;
        }

        // Option B: Call a local Python embedding microservice (if user runs one)
        public async Task<float[]> GetEmbeddingLocalAsync(string text, string localUrl = "http://localhost:5001/embed")
        {
            var client = _httpFactory.CreateClient();
            var payload = new { text };
            var resp = await client.PostAsync(localUrl, new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();
            var j = JsonConvert.DeserializeObject<dynamic>(await resp.Content.ReadAsStringAsync());
            var arr = ((IEnumerable<object>)j.embedding).Select(x => Convert.ToSingle(x)).ToArray();
            return arr;
        }
    }
}