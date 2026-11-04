namespace MapApp.Api.Validation;

/// <summary>
/// Collects field-level validation errors keyed by a JSON-like path (e.g. <c>[0].geometry.coordinates</c>).
/// </summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool HasErrors => _errors.Count > 0;

    public void Add(string path, string message)
    {
        if (!_errors.TryGetValue(path, out var messages))
        {
            messages = [];
            _errors[path] = messages;
        }

        messages.Add(message);
    }

    /// <summary>Throws <see cref="RequestValidationException"/> if any error was collected.</summary>
    public void ThrowIfAny()
    {
        if (HasErrors)
        {
            throw new RequestValidationException(ToDictionary());
        }
    }

    public IDictionary<string, string[]> ToDictionary() =>
        _errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);
}

/// <summary>
/// Thrown when a request is valid JSON but violates business or geometry rules.
/// Translated to an HTTP 400 <c>ValidationProblemDetails</c> response.
/// </summary>
public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
