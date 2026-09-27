namespace MovieWatch.Domain.Common;

public enum FailureKind { Validation, Conflict, Unauthenticated, NotFound }

public sealed class OperationException(FailureKind kind, string message) : Exception(message)
{
    public FailureKind Kind { get; } = kind;
}
