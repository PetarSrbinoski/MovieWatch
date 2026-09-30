using System.ComponentModel.DataAnnotations;

namespace MovieWatch.Web.Request;

public record CreateImportJobRequest(
    [Range(1, 5)] int PageCount
    );

public record UpdateImportJobRequest(
    [Range(1, 5)] int PageCount,
    int Version
    );
