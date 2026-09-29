using ClosedXML.Excel;
using MovieWatch.Domain.Dto;
using MovieWatch.Service.Interface;

namespace MovieWatch.Service.Implementation;

public sealed class WorkbookExportService : IWorkbookExportService
{
    public byte[] CreateWorkbook(RecommendationResultDto result)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Recommendations");
        Text(sheet, 1, 1, "MovieWatch recommendations");
        Text(sheet, 2, 1, "Mood");
        Text(sheet, 2, 2, result.Mood.Name);
        Text(sheet, 3, 1, "Filters");
        Text(sheet, 3, 2, result.Query.GenreIds.Count == 0 ? "Any genre" : string.Join(", ", result.Query.GenreIds));
        Text(sheet, 4, 1, "Maximum runtime (minutes)");
        sheet.Cell(4, 2).Value = result.Query.MaximumRuntimeMinutes;
        Text(sheet, 5, 1, "Include watched");
        Text(sheet, 5, 2, result.Query.IncludeWatched ? "Yes" : "No");
        Text(sheet, 6, 1, "Generated (UTC)");
        Text(sheet, 6, 2, result.GeneratedAt.UtcDateTime.ToString("O"));
        Text(sheet, 7, 1, "This product uses the TMDB API but is not endorsed or certified by TMDB.");
        var headers = new List<string> { "Title", "Runtime (minutes)", "Genres", "Score", "Explanation" };
        headers.AddRange(result.ParticipatingViewerIds.Select(id => $"Member {id} score"));
        for (var column = 0; column < headers.Count; column++)
            Text(sheet, 9, column + 1, headers[column]);
        sheet.Range(9, 1, 9, headers.Count).Style.Font.Bold = true;
        var row = 10;
        foreach (var recommendation in result.Recommendations)
        {
            Text(sheet, row, 1, recommendation.Movie.Title);
            if (recommendation.Movie.RuntimeMinutes is { } runtime)
                sheet.Cell(row, 2).Value = runtime;
            Text(sheet, row, 3, string.Join(", ", recommendation.Movie.Genres.Select(g => g.Name)));
            sheet.Cell(row, 4).Value = recommendation.Score;
            Text(sheet, row, 5, recommendation.Explanation);
            for (var index = 0; index < result.ParticipatingViewerIds.Count; index++)
            {
                var memberId = result.ParticipatingViewerIds[index];
                var contribution = recommendation.MemberScores.Single(m => m.ViewerId == memberId);
                sheet.Cell(row, 6 + index).Value = contribution.Score;
            }
            row++;
        }
        sheet.Columns(1, Math.Max(5, headers.Count)).AdjustToContents(1, Math.Min(row, 30));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Text(IXLWorksheet sheet, int row, int column, string value)
    {
        var cell = sheet.Cell(row, column);
        cell.Style.NumberFormat.Format = "@";
        cell.Value = value;
    }
}
