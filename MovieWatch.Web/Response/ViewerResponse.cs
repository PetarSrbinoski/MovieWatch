namespace MovieWatch.Web.Response;

public record ViewerResponse(
    Guid Id,
    string Email,
    string DisplayName
    );
