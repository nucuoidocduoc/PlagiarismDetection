using System.Text.Encodings.Web;

namespace PlagiarismDetection.Services
{
    public class ReportService
    {
        public string RenderHtml(string inputText, IEnumerable<dynamic> matches)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<html><head><meta charset='utf-8' /><style>body{font-family:Arial}\n.match{background:#ffef99;padding:4px;border-radius:4px}</style></head><body>");
            sb.AppendLine("<h1>Plagiarism Report</h1>");
            sb.AppendLine("<h2>Input</h2><pre>" + HtmlEncoder.Default.Encode(inputText) + "</pre>");
            sb.AppendLine("<hr/><h2>Matches</h2>");
            foreach (var m in matches)
            {
                sb.AppendLine($"<div><h3>Score: {m.Score:F3}</h3><p><strong>Source:</strong> {HtmlEncoder.Default.Encode((string)m.Source ?? "")}</p><p class='match'>{HtmlEncoder.Default.Encode((string)m.Text ?? "")}</p></div><hr/>");
            }
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        // Optionally convert HTML -> PDF using DinkToPdf or external tool
    }
}