using System.ComponentModel.DataAnnotations;

namespace MovieWatch.Web.Request;

public sealed record CreateImportJobRequest([Range(1, 5)] int PageCount);
public sealed record UpdateImportJobRequest([Range(1, 5)] int PageCount, int Version);
