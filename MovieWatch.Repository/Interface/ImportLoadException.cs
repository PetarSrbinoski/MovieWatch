namespace MovieWatch.Repository.Interface;

public sealed class ImportLoadException(string message, Exception innerException) : Exception(message, innerException);
