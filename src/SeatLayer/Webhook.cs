using System.Security.Cryptography;
using System.Text;

namespace SeatLayer;

/// <summary>
/// Webhook signature verification.
/// </summary>
/// <remarks>
/// <para>
/// The most security-sensitive thing an integrator writes by hand, and the two classic
/// mistakes are both easy to make and silent:
/// </para>
/// <list type="number">
///   <item>verifying against a re-serialised body, which changes bytes and fails — or
///   worse, gets "fixed" by skipping verification entirely;</item>
///   <item>comparing signatures with <c>==</c>, which leaks the expected value through
///   timing.</item>
/// </list>
/// <para>So the SDK does it, takes the RAW body, and compares in constant time.</para>
/// </remarks>
public static class Webhook
{
    /// <summary>
    /// Verifies a delivery and returns its decoded payload.
    /// </summary>
    /// <param name="payload">
    /// The RAW request body. In ASP.NET Core, enable buffering and read
    /// <c>Request.Body</c> directly — never a model-bound object re-serialised.
    /// </param>
    /// <param name="signature">The <c>X-SeatLayer-Signature</c> header value.</param>
    /// <param name="secret">The signing secret from webhook creation.</param>
    /// <remarks>
    /// NOTE ON REPLAY: deliveries are signed over the body, which carries an <c>at</c>
    /// timestamp — but nothing enforces a freshness window, so a captured delivery stays
    /// valid indefinitely. Replay protection is yours: every event carries an
    /// <c>occurrenceId</c>, and the correct pattern is to record processed ids and ignore
    /// repeats. Do not skip this.
    /// </remarks>
    /// <exception cref="SeatLayerWebhookVerificationException">The delivery is not ours.</exception>
    public static IReadOnlyDictionary<string, object?> Verify(
        ReadOnlySpan<byte> payload, string? signature, string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new SeatLayerWebhookVerificationException("A webhook signing secret is required.");
        }

        if (string.IsNullOrEmpty(signature))
        {
            throw new SeatLayerWebhookVerificationException("Missing X-SeatLayer-Signature header.");
        }

        var separator = signature.IndexOf('=');
        if (separator < 0
            || !signature.AsSpan(0, separator).SequenceEqual("sha256")
            || separator == signature.Length - 1)
        {
            throw new SeatLayerWebhookVerificationException(
                $"Unsupported signature format \"{signature}\"; expected \"sha256=<hex>\".");
        }

        var provided = signature[(separator + 1)..];
        var expected = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload)).ToLowerInvariant();

        // FixedTimeEquals is constant time and handles a length mismatch without leaking
        // which of the two failures occurred.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
        {
            throw new SeatLayerWebhookVerificationException("Webhook signature did not match.");
        }

        try
        {
            return Json.ToDictionary(Encoding.UTF8.GetString(payload));
        }
        catch (Exception error)
        {
            throw new SeatLayerWebhookVerificationException(
                $"Signature verified but the body is not valid JSON: {error.Message}");
        }
    }

    /// <summary>Convenience overload for callers holding the body as a string.</summary>
    public static IReadOnlyDictionary<string, object?> Verify(
        string payload, string? signature, string secret)
        => Verify(Encoding.UTF8.GetBytes(payload), signature, secret);
}
