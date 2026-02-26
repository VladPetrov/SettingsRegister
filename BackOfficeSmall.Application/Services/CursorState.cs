using BackOfficeSmall.Application.Exceptions;
using System.Text;

namespace BackOfficeSmall.Application.Services;

public sealed record CursorState(DateTime ChangedAtUtc, Guid Id)
{
    private const int CursorPartsCount = 2;

    public string EncodeCursor()
    {
        var payload = $"{ChangedAtUtc.Ticks}|{Id:D}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    public static CursorState? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var normalized = cursor
                .Replace("-", "+", StringComparison.Ordinal)
                .Replace("_", "/", StringComparison.Ordinal);

            var padded = normalized.PadRight(normalized.Length + ((4 - (normalized.Length % 4)) % 4), '=');
            var bytes = Convert.FromBase64String(padded);
            var payload = Encoding.UTF8.GetString(bytes);
            var parts = payload.Split('|', StringSplitOptions.None);

            if (parts.Length != CursorPartsCount)
            {
                throw new ValidationException("cursor has an invalid format.");
            }

            if (!long.TryParse(parts[0], out var ticks))
            {
                throw new ValidationException("cursor has an invalid timestamp.");
            }

            if (!Guid.TryParse(parts[1], out var id))
            {
                throw new ValidationException("cursor has an invalid identifier.");
            }

            var changedAtUtc = new DateTime(ticks, DateTimeKind.Utc);
            return new CursorState(changedAtUtc, id);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ValidationException("cursor has an invalid format.");
        }
    }
}