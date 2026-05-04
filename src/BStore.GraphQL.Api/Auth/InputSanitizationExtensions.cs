using System.Text.RegularExpressions;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Input sanitization utilities for XSS and injection prevention at the GraphQL boundary.
/// Apply to string inputs in mutations before they reach the service/data layer.
/// </summary>
public static partial class InputSanitizationExtensions
{
    // Matches HTML tags, script tags, and common XSS vectors
    [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagPattern();

    // Matches SQL injection patterns (common keywords in suspicious positions)
    [GeneratedRegex(@"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|ALTER|EXEC|UNION|CREATE)\b\s)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SqlInjectionPattern();

    /// <summary>Strip HTML tags from input to prevent XSS.</summary>
    public static string StripHtml(this string? input) =>
        string.IsNullOrWhiteSpace(input)
            ? string.Empty
            : HtmlTagPattern().Replace(input, string.Empty).Trim();

    /// <summary>Sanitize a string input: strip HTML, trim, and enforce max length.</summary>
    public static string Sanitize(this string? input, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var cleaned = input.StripHtml();
        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }

    /// <summary>Check if a string contains potential SQL injection patterns.</summary>
    public static bool ContainsSqlInjection(this string? input) =>
        !string.IsNullOrWhiteSpace(input) && SqlInjectionPattern().IsMatch(input);

    /// <summary>
    /// Validate and sanitize an email address.
    /// Returns the sanitized email or throws if format is invalid.
    /// </summary>
    public static string SanitizeEmail(this string? email)
    {
        var cleaned = email.Sanitize(254);
        if (string.IsNullOrEmpty(cleaned))
            throw new ArgumentException("Email address is required.");

        if (!cleaned.Contains('@') || !cleaned.Contains('.'))
            throw new ArgumentException("Invalid email address format.");

        return cleaned.ToLowerInvariant();
    }

    /// <summary>
    /// Sanitize a search query string — strip dangerous characters, enforce max length.
    /// </summary>
    public static string SanitizeSearchQuery(this string? query, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        var cleaned = query.StripHtml();

        if (cleaned.ContainsSqlInjection())
            throw new ArgumentException("Search query contains invalid characters.");

        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }
}
