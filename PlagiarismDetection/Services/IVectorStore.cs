namespace PlagiarismDetection.Services
{
    public interface IVectorStore
    {
        Task UpsertAsync(string id, float[] vector, object metadata);

        Task<IEnumerable<(string Id, float Score, dynamic Metadata)>> QueryAsync(float[] vector, ulong topK = 10);
    }
}