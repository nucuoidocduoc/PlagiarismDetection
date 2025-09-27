using PlagiarismDetection.Models;
using System.Xml.Linq;

namespace PlagiarismDetection.Services
{
    public class CrawlerService
    {
        private readonly IHttpClientFactory _httpFactory;

        public CrawlerService(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        // Simple arXiv search using export API
        public async Task<IEnumerable<Document>> FetchArxivAsync(string query, int maxResults = 100)
        {
            var client = _httpFactory.CreateClient();
            var url = $"http://export.arxiv.org/api/query?search_query={Uri.EscapeDataString(query)}&start=0&max_results={maxResults}";
            var resp = await client.GetStringAsync(url);
            var doc = XDocument.Parse(resp);
            var entries = doc.Descendants("entry").Select(e => new Document
            {
                Id = e.Element("id")?.Value ?? Guid.NewGuid().ToString(),
                Title = e.Element("title")?.Value?.Trim() ?? string.Empty,
                Authors = e.Elements("author").Select(a => a.Element("name")?.Value?.Trim() ?? string.Empty).ToArray(),
                Text = e.Element("summary")?.Value?.Trim() ?? string.Empty,
                Source = "arXiv"
            });
            return entries;
        }

        // PubMed via Entrez E-utilities (efetch/esearch)
        public async Task<IEnumerable<Document>> FetchPubMedAsync(string query, int retmax = 100)
        {
            var client = _httpFactory.CreateClient();
            var esearch = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?db=pubmed&retmax={retmax}&term={Uri.EscapeDataString(query)}";
            var esearchResp = await client.GetStringAsync(esearch);
            var ids = ParseIdsFromEsearch(esearchResp);
            var results = new List<Document>();
            foreach (var id in ids)
            {
                var efetch = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=pubmed&id={id}&retmode=xml";
                var xml = await client.GetStringAsync(efetch);
                // naive parse for ArticleTitle and AbstractText
                var xdoc = XDocument.Parse(xml);
                var article = xdoc.Descendants("Article").FirstOrDefault();
                if (article != null)
                {
                    var title = article.Element("ArticleTitle")?.Value ?? string.Empty;
                    var abstractText = string.Join(" ", article.Descendants("AbstractText").Select(x => x.Value));
                    results.Add(new Document { Id = id, Title = title, Text = abstractText, Source = "PubMed" });
                }
            }
            return results;
        }

        private IEnumerable<string> ParseIdsFromEsearch(string xml)
        {
            var x = XDocument.Parse(xml);
            return x.Descendants("Id").Select(i => i.Value).ToList();
        }
    }
}