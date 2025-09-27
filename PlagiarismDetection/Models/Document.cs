namespace PlagiarismDetection.Models
{
    public class Document
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string[] Authors { get; set; } = [];
        public string Text { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // arXiv / PubMed / uploaded
    }
}