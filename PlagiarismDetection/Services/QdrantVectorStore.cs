using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace PlagiarismDetection.Services
{
    public class QdrantVectorStore : IVectorStore
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly string _qdrantUrl;
        private readonly string _collection = "plagiarism_chunks";

        public QdrantVectorStore(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _qdrantUrl = config["QDRANT_URL"] ?? "http://localhost:6333";
            EnsureCollectionAsync().GetAwaiter().GetResult();
        }

        private HttpClient CreateClient()
        {
            var c = _httpFactory.CreateClient();
            return c;
        }

        private async Task EnsureCollectionAsync()
        {
            var client = CreateClient();
            var url = $"{_qdrantUrl}/collections/{_collection}";
            var r = await client.GetAsync(url);
            if (!r.IsSuccessStatusCode)
            {
                var body = new { vectors = new { size = 768, distance = "Cosine" } }; // size must match embedding size
                _ = await client.PutAsync(url, new StringContent(JsonConvert.SerializeObject(body), System.Text.Encoding.UTF8, "application/json"));
            }
        }

        public async Task UpsertAsync(string id, float[] vector, object metadata)
        {
            var client = CreateClient();
            var url = $"{_qdrantUrl}/collections/{_collection}/points?wait=true";
            var payload = new
            {
                points = new[] {
                    new QdrantPoint {Id= 1,Vector= vector, Payload = metadata }
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PutAsync(url, content);
            resp.EnsureSuccessStatusCode();
        }

        public class QdrantPoint
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("vector")]
            public float[] Vector { get; set; }

            [JsonProperty("payload")]
            public object Payload { get; set; }
        }

        public async Task<IEnumerable<(string Id, float Score, dynamic Metadata)>> QueryAsync(float[] vector, int topK = 10)
        {
            var client = CreateClient();
            var url = $"{_qdrantUrl}/collections/{_collection}/points/search";
            var payload = new { vector, limit = topK };
            var resp = await client.PostAsync(url, new StringContent(JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json"));
            var txt = await resp.Content.ReadAsStringAsync();
            var j = JObject.Parse(txt);
            var results = new List<(string, float, dynamic)>();
            foreach (var p in j["result"])
            {
                var id = p["id"].ToString();
                var score = (float)p["score"];
                var payloadObj = p["payload"];
                results.Add((id, score, payloadObj));
            }
            return results;
        }
    }
}