using MapApp.Api.Validation;

namespace MapApp.Api.Services;

/// <summary>
/// Validation rules shared by the polygon and map-object services.
/// </summary>
internal static class RequestRules
{
    public const int MaxBatchSize = 500;
    public const int MaxNameLength = 100;

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
}
