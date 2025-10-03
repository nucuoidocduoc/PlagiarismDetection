using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace PlagiarismDetection.Services
{
    public class QdrantVectorStore : IVectorStore
    {
        private readonly string _collection = "plagiarism_chunks";

        public QdrantVectorStore(IConfiguration config)
        {
            EnsureCollectionAsync().GetAwaiter().GetResult();
        }

        private QdrantClient CreateClient()
        {
            return new QdrantClient("localhost", 6334);
        }

        private async Task EnsureCollectionAsync()
        {
            var client = CreateClient();
            try
            {
                var collectionInfo = await client.GetCollectionInfoAsync(_collection);

                Console.WriteLine($"Collection {_collection} đã tồn tại");
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                await client.CreateCollectionAsync(
                    collectionName: _collection,
                    vectorsConfig: new VectorParams
                    {
                        Size = 1024,
                        Distance = Distance.Cosine
                    }
                );
                Console.WriteLine($"Đã tạo collection {_collection}");
            }
        }

        public async Task UpsertAsync(string id, float[] vector, object metadata)
        {
            var client = CreateClient();
            var point = new PointStruct()
            {
                Id = new PointId() { Uuid = id },
                Vectors = vector,
            };

            foreach (var kv in metadata.ToQdrantKeyValue())
            {
                point.Payload.Add(kv.Key, kv.Value);
            }

            _ = await client.UpsertAsync(_collection, [point]);
        }

        public async Task<IEnumerable<(string Id, float Score, dynamic Metadata)>> QueryAsync(float[] vector, ulong topK = 10)
        {
            var client = CreateClient();
            var points = await client.SearchAsync(_collection, vector, limit: topK);
            var results = new List<(string, float, dynamic)>();
            foreach (var point in points)
            {
                results.Add((point.Id.Uuid.ToString(), point.Score, point.Payload.ToSepicificDictionary()));
            }

            return results;
        }
    }
}