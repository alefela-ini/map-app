namespace MapApp.Api.Models;

/// <summary>
/// A MongoDB document with a string representation of its <c>ObjectId</c>.
/// </summary>
public interface IDocument
{
    string Id { get; }
}
