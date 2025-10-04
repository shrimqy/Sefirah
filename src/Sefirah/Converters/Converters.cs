using System.Globalization;
using Microsoft.UI.Xaml.Data;
using Sefirah.Data.Models;
using Sefirah.Extensions;

namespace Sefirah.Converters;

/// <summary>
/// The generic base implementation of a value converter.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target type.</typeparam>
internal abstract class ValueConverter<TSource, TTarget> : IValueConverter
{
    /// <summary>
    /// Converts a source value to the target type.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public TTarget? Convert(TSource? value)
    {
        return Convert(value, null, null);
    }

    /// <summary>
    /// Converts a target value back to the source type.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public TSource? ConvertBack(TTarget? value)
    {
        return ConvertBack(value, null, null);
    }

    /// <summary>
    /// Modifies the source data before passing it to the target for display in the UI.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public object? Convert(object? value, Type? targetType, object? parameter, string? language)
    {
        // CastExceptions will occur when invalid value, or target type provided.
        return Convert((TSource?)value, parameter, language);
    }

    /// <summary>
    /// Modifies the target data before passing it to the source object. This method is called only in TwoWay bindings.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public object? ConvertBack(object? value, Type? targetType, object? parameter, string? language)
    {
        // CastExceptions will occur when invalid value, or target type provided.
        return ConvertBack((TTarget?)value, parameter, language);
    }

    /// <summary>
    /// Converts a source value to the target type.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameter"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    protected virtual TTarget? Convert(TSource? value, object? parameter, string? language)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Converts a target value back to the source type.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameter"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    protected virtual TSource? ConvertBack(TTarget? value, object? parameter, string? language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// The base class for converting instances of type T to object and vice versa.
/// </summary>
internal abstract class ToObjectConverter<T> : ValueConverter<T?, object?>
{
    /// <summary>
    /// Converts a source value to the target type.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameter"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    protected override object? Convert(T? value, object? parameter, string? language)
    {
        return value;
    }

    /// <summary>
    /// Converts a target value back to the source type.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameter"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    protected override T? ConvertBack(object? value, object? parameter, string? language)
    {
        return (T?)value;
    }
}

internal sealed partial class EmptyObjectToBooleanConverter : ValueConverter<object?, bool>
{
    public bool Inverse { get; set; }

    protected override bool Convert(object? value, object? parameter, string? language)
    {
        return Inverse ? value is not null : value is null;
    }

    protected override object? ConvertBack(bool value, object? parameter, string? language)
    {
        return null;
    }
}

internal sealed partial class EmptyCollectionToBooleanConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count && count == 0)
        {
            return !Inverse;
        }
        return Inverse;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class EmptyObjectToVisibilityConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (Inverse)
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        else
            return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class BooleanToVisibilityConverter : ValueConverter<bool, Visibility>
{
    public bool Inverse { get; set; }

    protected override Visibility Convert(bool value, object? parameter, string? language)
    {
        return Inverse ? !value ? Visibility.Visible : Visibility.Collapsed : value ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override bool ConvertBack(Visibility value, object? parameter, string? language)
    {
        throw new NotSupportedException();
    }
}

internal sealed partial class EmptyCollectionToVisibilityConverter : ValueConverter<IEnumerable, Visibility>
{
    public bool Inverse { get; set; }

    protected override Visibility Convert(IEnumerable? value, object? parameter, string? language)
    {
        bool isEmpty = value == null || !value.GetEnumerator().MoveNext();

        if (Inverse)
            return isEmpty ? Visibility.Visible : Visibility.Collapsed;
        else
            return isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    protected override IEnumerable? ConvertBack(Visibility value, object? parameter, string? language)
    {
        throw new NotSupportedException();
    }
}

internal sealed partial class DateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string timestampStr && DateTime.TryParseExact(timestampStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
        {
            if (timestamp.Date == DateTime.Today)
            {
                // Return only the time if the date is the same as today
                return timestamp.ToString("t"); // Short time pattern
            }
            else
            {
                // Return the short date and time pattern otherwise
                return timestamp.ToString("g"); // Short date and time pattern
            }
        }

        return string.Empty; // Return an empty string if the timestamp is null or invalid
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class DateTimeDevicesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime timestamp)
        {
            // Check if the date is today
            if (timestamp.Date == DateTime.Today)
            {
                // Return only the time if the date is the same as today
                return timestamp.ToString("t", CultureInfo.CurrentCulture); // Short time pattern
            }
            else
            {
                // Return the short date and time pattern otherwise
                return timestamp.ToString("g", CultureInfo.CurrentCulture); // Short date and time pattern
            }
        }

        return string.Empty; // Return an empty string if the value is not a DateTime
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class BatteryStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DeviceStatus deviceStatus)
        {
            // Based on battery level and charging state, choose the appropriate icon
            if (deviceStatus.ChargingStatus)
            {
                return deviceStatus.BatteryStatus switch
                {
                    >= 100 => "\uEA93",
                    >= 90 => "\uE83E",
                    >= 80 => "\uE862",
                    >= 70 => "\uE861",
                    >= 60 => "\uE860",
                    >= 50 => "\uE85F",
                    >= 40 => "\uE85E",
                    >= 30 => "\uE85D",
                    >= 20 => "\uE85C",
                    >= 10 => "\uE85B",
                    _ => "\uE85A"
                };
            }
            else
            {
                return deviceStatus.BatteryStatus switch
                {
                    >= 100 => "\uE83F",
                    >= 90 => "\uE859",
                    >= 80 => "\uE858",
                    >= 70 => "\uE857",
                    >= 60 => "\uE856",
                    >= 50 => "\uE855",
                    >= 40 => "\uE854",
                    >= 30 => "\uE853",
                    >= 20 => "\uE852",
                    >= 10 => "\uE851",
                    _ => "\uE850"
                };
            }
        }

        return "\uE83F"; // Default icon
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for handling string equality to visibility
/// </summary>
internal sealed partial class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string givenString && parameter is string targetString)
        {
            return givenString.Equals(targetString, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class RingerModeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int ringerMode)
        {
            return ringerMode switch
            {
                2 => "\uE995",    // Normal (Speaker icon)
                1 => "\uE877",    // Vibrate icon
                0 => "\uE74F",    // Silent (Mute icon)
                _ => "\uE995"     // Default to speaker icon
            };
        }

        return "\uE995"; // Default icon
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class MessageTypeToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // values: 1 = INBOX, 2 = SENT 
        int messageType = (int)value;
        return messageType == 2 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class MessageTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // INBOX (and other received types) should be Visible, SENT should be Collapsed.
        return (int)value != 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class InverseMessageTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // SENT should be Visible, INBOX (and others) should be Collapsed.
        return (int)value == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class UnixTimestampConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long unixTimestampMs)
        {
            // Convert Unix timestamp (milliseconds) to DateTime
            DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestampMs).LocalDateTime;

            // Check if parameter is provided for full format (with time always included)
            if (parameter?.ToString() == "F")
            {
                // Format based on how recent the message is, but always include time
                if (dateTime.Date == DateTime.Today)
                {
                    // Today - show "Today" + time
                    return "Today " + dateTime.ToString("h:mm tt");
                }
                else if (dateTime.Date == DateTime.Today.AddDays(-1))
                {
                    // Yesterday - show "Yesterday" + time
                    return "Yesterday " + dateTime.ToString("h:mm tt");
                }
                else if (dateTime.Date > DateTime.Today.AddDays(-7))
                {
                    // Within the last week - show day + time
                    return dateTime.ToString("dddd") + " " + dateTime.ToString("h:mm tt"); // Full day name + time
                }
                else if (dateTime.Year == DateTime.Today.Year)
                {
                    // This year - show full month, day + time
                    return dateTime.ToString("MMMM d") + " " + dateTime.ToString("h:mm tt");
                }
                else
                {
                    // Older - show full month, day, year + time
                    return dateTime.ToString("MMMM d, yyyy") + " " + dateTime.ToString("h:mm tt");
                }
            }

            // Format based on how recent the message is (default behavior)
            if (dateTime.Date == DateTime.Today)
            {
                // Today - show only time
                return dateTime.ToString("t"); // Short time pattern (e.g., 3:15 PM)
            }
            else if (dateTime.Date == DateTime.Today.AddDays(-1))
            {
                // Yesterday
                return "Yesterday " + dateTime.ToString("t");
            }
            else if (dateTime.Date > DateTime.Today.AddDays(-7))
            {
                // Within the last week
                return dateTime.ToString("ddd") + " " + dateTime.ToString("t"); // Day abbreviation (e.g., Mon) + time
            }
            else if (dateTime.Year == DateTime.Today.Year)
            {
                // This year
                return dateTime.ToString("MMM d"); // Month abbreviation + day (e.g., Jul 15)
            }
            else
            {
                // Older
                return dateTime.ToString("MMM d, yyyy"); // Month abbreviation + day + year (e.g., Jul 15, 2023)
            }
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class IndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int intValue)
        {
            // Convert from 1-based to 0-based index
            return intValue - 1;
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is int intValue)
        {
            return intValue + 1;
        }
        return 1;
    }
}

internal sealed partial class GreaterThanOneToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is int count && count > 1;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

internal sealed partial class SubscriptionToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int subscription)
        {
            return subscription switch
            {
                1 => "\uE884",
                2 => "\uE882",
                _ => "\uE884"
            };
        }
        return "\uE884";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
