namespace GlazeShell.Core.Models;

internal static class ModelValidation
{
    public static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    public static string? Optional(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    public static void NonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value cannot be negative.");
        }
    }

    public static void NonNegative(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be a finite non-negative number.");
        }
    }

    public static void Positive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be a finite positive number.");
        }
    }

    public static void InRange(double value, double minimum, double maximum, string parameterName)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"The value must be between {minimum} and {maximum}.");
        }
    }

    public static IReadOnlyList<T> Copy<T>(IEnumerable<T>? values, string parameterName)
        where T : class
    {
        if (values is null)
        {
            return Array.Empty<T>();
        }

        var copy = values.ToArray();
        if (copy.Any(static value => value is null))
        {
            throw new ArgumentException("The collection cannot contain null items.", parameterName);
        }

        return copy;
    }
}
