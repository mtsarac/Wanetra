using Wanetra.Domain;

namespace Wanetra.Application.SpeedTests;

public sealed class SpeedTestExecutionException(
    SpeedTestFailureKind failureKind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public SpeedTestFailureKind FailureKind { get; } = failureKind;
}
