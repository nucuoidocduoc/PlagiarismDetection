using PlagiarismDetection.Models;

namespace PlagiarismDetection.Services
{
    public class TextProcessor
    {
        // chunk size in words
        private readonly int _chunkSize;

        public TextProcessor(int chunkSize = 120)
        {
            _chunkSize = chunkSize;
        }

        public IEnumerable<Chunk> ChunkDocument(Document doc)
        {
            var words = doc.Text.Split(new char[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<Chunk>();
            for (int i = 0, idx = 0; i < words.Length; i += _chunkSize, idx++)
            {
                var take = words.Skip(i).Take(_chunkSize);
                var txt = string.Join(' ', take);
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    chunks.Add(new Chunk { DocumentId = doc.Id, Text = txt, Index = idx });
                }
            }
            return chunks;
        }

        // PDF -> text (PdfPig)
        public string ExtractTextFromPdf(Stream pdfStream)
        {
            using var reader = UglyToad.PdfPig.PdfDocument.Open(pdfStream);
            var sb = new System.Text.StringBuilder();
            foreach (var page in reader.GetPages())
            {
                sb.AppendLine(page.Text);
            }
            return sb.ToString();
        }
    }
}