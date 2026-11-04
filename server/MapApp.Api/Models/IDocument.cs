namespace MapApp.Api.Models;

/// <summary>
/// A MongoDB document with a string representation of its <c>ObjectId</c>.
/// </summary>
public interface IDocument
{
    string Id { get; }

    /// <summary>
    /// Idempotency key chosen by the client (the draft id). Saving the same key again does not create a second
    /// document, so retrying a Save whose response was lost is safe. <c>null</c> for documents saved without one.
    /// </summary>
    string? ClientId { get; }
}
