using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace CrawlerBoKH
{
    public static class Crawler
    {
        private static readonly HttpClient client = new();
        private static List<ScientificProject> allProjects = [];
        private static readonly object lockObj = new object();
        private static int totalCrawled = 0;
        private static int totalErrors = 0;
        private static int currentPage = 0;

        // Cấu hình
        private const int PAGE_INCREMENT = 20;

        private const int MAX_CONCURRENT_REQUESTS = 2;
        private const int DELAY_BETWEEN_REQUESTS_MS = 1500;
        private const int DELAY_BETWEEN_DETAILS_MS = 800;
        private const int RETRY_ATTEMPTS = 3;
        private const int SAVE_INTERVAL = 50;

        public static async Task Start()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("  STI Vista - Crawl Toàn Bộ Dữ Liệu Đề Tài Khoa Học");
            Console.WriteLine("═══════════════════════════════════════════════════════════\n");

            // Cấu hình HTTP client
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
            client.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9");

            try
            {
                LoadExistingData();
                var startTime = DateTime.Now;

                // Tìm tổng số trang
                int totalPages = await FindTotalPages();
                Console.WriteLine($"📊 Tổng số trang: {totalPages}");
                Console.WriteLine($"📊 Dự kiến: ~{totalPages * 20} đề tài\n");

                // Tạo danh sách các page cần crawl
                var pageOffsets = new List<int>();
                for (int i = 0; i <= totalPages * PAGE_INCREMENT; i += PAGE_INCREMENT)
                {
                    pageOffsets.Add(i);
                }

                Console.WriteLine($"🚀 Bắt đầu crawl {pageOffsets.Count} trang...\n");

                // Crawl từng page với concurrency
                await ProcessPagesWithConcurrency(pageOffsets);

                // Lưu kết quả cuối cùng
                await SaveResults(force: true);

                var endTime = DateTime.Now;
                var duration = endTime - startTime;

                Console.WriteLine("\n═══════════════════════════════════════════════════════════");
                Console.WriteLine("  ✅ HOÀN THÀNH CRAWL DỮ LIỆU");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine($"  📁 Tổng đề tài: {allProjects.Count}");
                Console.WriteLine($"  ⚠️  Lỗi: {totalErrors}");
                Console.WriteLine($"  ⏱️  Thời gian: {duration.Hours}h {duration.Minutes}m {duration.Seconds}s");
                Console.WriteLine($"  💾 File output:");
                Console.WriteLine($"     - scientific_projects.json");
                Console.WriteLine($"     - scientific_projects.csv");
                Console.WriteLine($"     - scientific_projects_full.json (chi tiết đầy đủ)");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ LỖI: {ex.Message}");
                await SaveResults(force: true);
            }
        }

        private static async Task<int> FindTotalPages()
        {
            try
            {
                // Thử crawl page cao để tìm page cuối
                for (int testPage = 14880; testPage >= 0; testPage -= 100)
                {
                    string url = $"https://sti.vista.gov.vn/?mod=projects&q=&ts=all&af=&nb=0&tp=3&ministry=&local=&cat=&sort=&page={testPage}";
                    string html = await client.GetStringAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var projectRows = doc.DocumentNode.SelectNodes("//table[@id='tbl-data-kq']//tr[position()>1]");
                    if (projectRows != null && projectRows.Count > 0)
                    {
                        // Tìm thấy page có dữ liệu, return page này + một chút buffer
                        return (testPage / PAGE_INCREMENT) + 10;
                    }
                    await Task.Delay(500);
                }
                return 745; // Default fallback
            }
            catch
            {
                return 745; // Default fallback
            }
        }

        private static async Task ProcessPagesWithConcurrency(List<int> pageOffsets)
        {
            var semaphore = new SemaphoreSlim(MAX_CONCURRENT_REQUESTS);
            var tasks = new List<Task>();

            foreach (var offset in pageOffsets)
            {
                await semaphore.WaitAsync();

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await CrawlPageWithRetry(offset);

                        if (totalCrawled % SAVE_INTERVAL == 0)
                        {
                            await SaveResults();
                        }

                        await Task.Delay(DELAY_BETWEEN_REQUESTS_MS);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        private static async Task CrawlPageWithRetry(int offset)
        {
            for (int attempt = 1; attempt <= RETRY_ATTEMPTS; attempt++)
            {
                try
                {
                    await CrawlPage(offset);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == RETRY_ATTEMPTS)
                    {
                        Console.WriteLine($"❌ Page offset {offset} thất bại sau {RETRY_ATTEMPTS} lần thử: {ex.Message}");
                        Interlocked.Increment(ref totalErrors);
                    }
                    else
                    {
                        await Task.Delay(2000 * attempt);
                    }
                }
            }
        }

        private static async Task CrawlPage(int offset)
        {
            string url = $"https://sti.vista.gov.vn/?mod=projects&q=&ts=all&af=&nb=0&tp=3&ministry=&local=&cat=&sort=&page={offset}";
            string html = await client.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var projectRows = doc.DocumentNode.SelectNodes("//table[@id='tbl-data-kq']//tr[position()>1]");

            if (projectRows == null || projectRows.Count == 0)
            {
                return; // Hết dữ liệu
            }

            int projectsOnPage = 0;

            foreach (var row in projectRows)
            {
                try
                {
                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 3) continue;

                    var project = new ScientificProject
                    {
                        CrawledAt = DateTime.Now,
                        STT = int.TryParse(CleanText(cells[1].InnerText), out int stt) ? stt : 0
                    };

                    // Cell 2: Nội dung chi tiết
                    var contentCell = cells[2];

                    // Lấy title và detail URL
                    var titleLink = contentCell.SelectSingleNode(".//a[@target='_blank']");
                    if (titleLink != null)
                    {
                        project.Title = CleanText(titleLink.InnerText);
                        project.DetailUrl = titleLink.GetAttributeValue("href", "");

                        if (!string.IsNullOrEmpty(project.DetailUrl) && !project.DetailUrl.StartsWith("http"))
                        {
                            project.DetailUrl = "https://sti.vista.gov.vn" + project.DetailUrl;
                        }
                    }

                    // Crawl chi tiết từ trang detail
                    if (!string.IsNullOrEmpty(project.DetailUrl))
                    {
                        var success = await CrawlProjectDetails(project);
                        if (success)
                        {
                            lock (lockObj)
                            {
                                allProjects.Add(project);
                                projectsOnPage++;
                            }
                        }
                        await Task.Delay(DELAY_BETWEEN_DETAILS_MS);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️  Lỗi parse project: {ex.Message}");
                }
            }

            Interlocked.Add(ref totalCrawled, projectsOnPage);
            int pageNum = (offset / PAGE_INCREMENT) + 1;
            Console.WriteLine($"✅ Trang {pageNum}: +{projectsOnPage} đề tài | Tổng: {totalCrawled}");
        }

        private static async Task<bool> CrawlProjectDetails(ScientificProject project)
        {
            try
            {
                string html = await client.GetStringAsync(project.DetailUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Parse tất cả form-group theo cấu trúc thực tế
                var formGroups = doc.DocumentNode.SelectNodes("//div[@class='form-group']");

                if (formGroups != null)
                {
                    foreach (var group in formGroups)
                    {
                        var label = group.SelectSingleNode(".//label");
                        var value = group.SelectSingleNode(".//p[@class='form-control-static'] | .//div[@class='form-control-static']");

                        if (label == null || value == null) continue;

                        string labelText = CleanText(label.InnerText).ToLower();
                        string valueText = CleanText(value.InnerText);

                        // Map các trường theo label
                        if (labelText.Contains("ms đề tài") || labelText.Contains("mã số đề tài"))
                            project.ProjectCode = valueText;
                        else if (labelText.Contains("giấy đăng ký kq số") && string.IsNullOrEmpty(project.RegistrationNumber))
                            project.RegistrationNumber = valueText;
                        else if (labelText.Contains("tên nhiệm vụ"))
                            project.Title = valueText;
                        else if (labelText.Contains("tổ chức chủ trì"))
                            project.HostOrganization = valueText;
                        else if (labelText.Contains("cơ quan chủ quản"))
                            project.ManagementAgency = valueText;
                        else if (labelText.Contains("cấp quản lý nhiệm vụ"))
                            project.ProjectLevel = valueText;
                        else if (labelText.Contains("chủ nhiệm nhiệm vụ"))
                            project.MainLeader = valueText;
                        else if (labelText.Contains("cán bộ phối hợp"))
                            project.CooperatingMembers = valueText;
                        else if (labelText.Contains("lĩnh vực nghiên cứu"))
                            project.ResearchField = valueText;
                        else if (labelText.Contains("thời gian bắt đầu"))
                            project.StartDate = valueText;
                        else if (labelText.Contains("thời gian kết thúc"))
                            project.EndDate = valueText;
                        else if (labelText.Contains("ngày được nghiệm thu"))
                            project.AcceptanceDate = valueText;
                        else if (labelText.Contains("ngày cấp") && !labelText.Contains("giấy"))
                            project.RegistrationDate = valueText;
                        else if (labelText.Contains("cơ quan cấp"))
                            project.IssuingAgency = valueText;
                        else if (labelText.Contains("tóm tắt"))
                            project.Abstract = GetFullHtmlText(value);
                        else if (labelText.Contains("ký hiệu kho"))
                            project.StorageCode = valueText;
                        else if (labelText.Contains("hiệu quả kinh tế"))
                            project.EconomicEfficiency = GetFullHtmlText(value);
                        else if (labelText.Contains("từ khoá") || labelText.Contains("từ khóa"))
                            project.Keywords = valueText;
                        else if (labelText.Contains("trạng thái"))
                            project.Status = valueText;
                        else if (labelText.Contains("áp dụng đối với"))
                            project.ApplicableTo = valueText;
                        else if (labelText.Contains("kết quả của đề tài được ứng dụng trong lĩnh vực"))
                            project.ApplicationField = valueText;
                        else if (labelText.Contains("kết quả của đề tài khoa học và công nghệ có được sử dụng"))
                            project.PracticalApplication = valueText;
                        else if (labelText.Contains("số lượng công bố"))
                        {
                            string fullText = valueText;
                            var match1 = Regex.Match(fullText, @"trong nước:\s*(\d+)");
                            var match2 = Regex.Match(fullText, @"quốc tế:\s*(\d+)");
                            if (match1.Success) project.DomesticPublications = match1.Groups[1].Value;
                            if (match2.Success) project.InternationalPublications = match2.Groups[1].Value;
                        }
                        else if (labelText.Contains("hình thành yêu cầu bảo hộ"))
                            project.IntellectualProperty = valueText;
                        else if (labelText.Contains("góp phần vào đào tạo"))
                            project.TrainingContribution = valueText;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️  Lỗi crawl detail: {ex.Message}");
                return false;
            }
        }

        private static string GetFullHtmlText(HtmlNode node)
        {
            if (node == null) return "";

            // Lấy toàn bộ text bao gồm cả các thẻ con
            var allParagraphs = node.SelectNodes(".//p");
            if (allParagraphs != null && allParagraphs.Count > 0)
            {
                return string.Join("\n", allParagraphs.Select(p => CleanText(p.InnerText)));
            }

            return CleanText(node.InnerText);
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = System.Net.WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ");
            return text.Trim();
        }

        private static void LoadExistingData()
        {
            try
            {
                if (File.Exists("scientific_projects_full.json"))
                {
                    string json = File.ReadAllText("scientific_projects_full.json");
                    var existing = JsonConvert.DeserializeObject<List<ScientificProject>>(json);
                    if (existing != null && existing.Count > 0)
                    {
                        allProjects = existing;
                        totalCrawled = existing.Count;
                        Console.WriteLine($"📂 Đã load {existing.Count} đề tài từ backup\n");
                    }
                }
            }
            catch { }
        }

        private static async Task SaveResults(bool force = false)
        {
            if (!force && allProjects.Count == 0) return;

            try
            {
                // Lưu JSON đầy đủ
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Include
                };
                string jsonFull = JsonConvert.SerializeObject(allProjects, settings);
                await File.WriteAllTextAsync("scientific_projects_full.json", jsonFull);

                // Lưu CSV
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("ProjectCode,RegistrationNumber,Title,HostOrganization,ManagementAgency,ProjectLevel,MainLeader,CooperatingMembers,ResearchField,StartDate,EndDate,AcceptanceDate,RegistrationDate,IssuingAgency,StorageCode,Keywords,Status,DomesticPublications,InternationalPublications,TrainingContribution,DetailUrl");

                foreach (var p in allProjects)
                {
                    csv.AppendLine($"{Csv(p.ProjectCode)},{Csv(p.RegistrationNumber)},{Csv(p.Title)}," +
                                  $"{Csv(p.HostOrganization)},{Csv(p.ManagementAgency)},{Csv(p.ProjectLevel)}," +
                                  $"{Csv(p.MainLeader)},{Csv(p.CooperatingMembers)},{Csv(p.ResearchField)}," +
                                  $"{Csv(p.StartDate)},{Csv(p.EndDate)},{Csv(p.AcceptanceDate)}," +
                                  $"{Csv(p.RegistrationDate)},{Csv(p.IssuingAgency)},{Csv(p.StorageCode)}," +
                                  $"{Csv(p.Keywords)},{Csv(p.Status)},{Csv(p.DomesticPublications)}," +
                                  $"{Csv(p.InternationalPublications)},{Csv(p.TrainingContribution)},{Csv(p.DetailUrl)}");
                }

                await File.WriteAllTextAsync("scientific_projects.csv", csv.ToString(),
                    new System.Text.UTF8Encoding(true));

                if (force)
                    Console.WriteLine($"\n💾 Đã lưu: {allProjects.Count} đề tài");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi lưu file: {ex.Message}");
            }
        }

        private static string Csv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "\"\"";
            text = text.Replace("\"", "\"\"");
            text = text.Replace("\n", " ").Replace("\r", " ");
            return $"\"{text}\"";
        }
    }
}