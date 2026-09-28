using MovieWatch.Domain.Common;

namespace MovieWatch.Service.Implementation;

internal static class Pagination
{
    public static void Validate(int skip, int take)
    {
        if (skip < 0)
            throw new OperationException(FailureKind.Validation, "Skip must be nonnegative.");
        if (take is < 1 or > 100)
            throw new OperationException(FailureKind.Validation, "Take must be 1 to 100.");
    }
}
