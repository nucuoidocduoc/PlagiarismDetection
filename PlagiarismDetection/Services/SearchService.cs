using Nest;
using PlagiarismDetection.Models;

namespace PlagiarismDetection.Services
{
    public class SearchService
    {
        private readonly ElasticClient _elasticClient;

        public SearchService(ElasticClient elasticClient)
        {
            _elasticClient = elasticClient;
        }

        public async Task<IEnumerable<Project>> SearchProject(string fieldName, string searchText)
        {
            var response = await _elasticClient.SearchAsync<Project>(s => s
                .Index("research-projects")
                .Size(10)
                .Query(q => q
                    .Match(m => m
                        .Field(fieldName)
                        .Query(searchText)
                    )
                )
                .Sort(sort => sort
                    .Descending(SortSpecialField.Score)
                )
            );

            if (response.IsValid)
            {
                return [.. response.Hits.Select(x => x.Source)];
            }

            return null;
        }
    }
}