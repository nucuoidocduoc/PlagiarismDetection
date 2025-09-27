namespace PlagiarismDetection.Models
{
    public class Chunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DocumentId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Index { get; set; }
    }
}