using MapApp.Api.Validation;

namespace MapApp.Api.Services;

/// <summary>
/// Validation rules shared by the polygon and map-object services.
/// </summary>
internal static class RequestRules
{
    public const int MaxBatchSize = 500;
    public const int MaxNameLength = 100;
    public const int MaxClientIdLength = 100;

    /// <summary>Validates that a batch is present, non-empty and not too large.</summary>
    public static void ValidateBatch<T>(IReadOnlyList<T>? items, ValidationErrors errors)
    {
        if (items is null || items.Count == 0)
        {
            errors.Add("$", "At least one item is required.");
        }
        else if (items.Count > MaxBatchSize)
        {
            errors.Add("$", $"At most {MaxBatchSize} items can be saved at once.");
        }
        else if (items.Any(i => i is null))
        {
            errors.Add("$", "Items must not be null.");
        }

        errors.ThrowIfAny();
    }

    /// <summary>Trims the name and falls back to <paramref name="fallback"/> when it is empty.</summary>
    public static string NormalizeName(string? name, string fallback, string path, ValidationErrors errors)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return fallback;
        }

        if (trimmed.Length > MaxNameLength)
        {
            errors.Add(path, $"Name must be at most {MaxNameLength} characters.");
        }

        return trimmed;
    }

    /// <summary>
    /// Validates the optional idempotency keys of a batch. A key repeated inside one batch is rejected:
    /// the upsert would treat the second item as "already saved" and silently drop it.
    /// </summary>
    public static void ValidateClientIds(IEnumerable<string?> clientIds, ValidationErrors errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var clientId in clientIds)
        {
            var path = $"[{index++}].clientId";
            if (clientId is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > MaxClientIdLength)
            {
                errors.Add(path, $"Client id must be 1 to {MaxClientIdLength} characters.");
            }
            else if (!seen.Add(clientId))
            {
                errors.Add(path, "Client id is repeated in this batch.");
            }
        }
    }
}
