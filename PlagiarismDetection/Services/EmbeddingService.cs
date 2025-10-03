using Newtonsoft.Json;
using System.Text;

namespace PlagiarismDetection.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;

        public EmbeddingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new System.Uri("http://localhost:11434"); // Ollama API
        }

        public async Task<float[]> GetEmbeddingOpenAiAsync(string text)
        {
            var payload = new
            {
                model = "mxbai-embed-large",
                prompt = text
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.PostAsync("/api/embeddings", content);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<OllamaEmbeddingResponse>(body);

            if (result?.Embedding == null)
                throw new System.Exception("Embedding not found in Ollama response.");

            return [.. result.Embedding];
        }
    }

    public class OllamaEmbeddingResponse
    {
        [JsonProperty("embedding")]
        public List<float> Embedding { get; set; }
    }
}