using SharedKernel.Concrete;

namespace Domain.Entities.Retention;

public static class RetentionErrors
{
    /// <summary>
    /// A table could not be trimmed to its retention window. Includes how many rows DID
    /// go before it stopped: partial progress plus a lock timeout is a transient problem
    /// that the next nightly run will finish, while zero progress usually is not.
    /// </summary>
    public static Error TrimFailed(string tableName, int deletedBeforeFailure, string detail) =>
        Error.Problem(
            "Retention.TrimFailed",
            $"Trimming {tableName} stopped after {deletedBeforeFailure} row(s): {detail}");
}
