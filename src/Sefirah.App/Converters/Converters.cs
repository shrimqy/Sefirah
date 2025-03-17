﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;
using Sefirah.App.Extensions;
using System.Globalization;

namespace Sefirah.App.Converters;

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

public class DateTimeConverter : IValueConverter
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

public class DateTimeDevicesConverter : IValueConverter
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

public class BatteryStatusToIconConverter : IValueConverter
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

internal sealed class StringNullOrWhiteSpaceToTrueConverter : ValueConverter<string, bool>
{
    /// <summary>
    /// Determines whether an inverse conversion should take place.
    /// </summary>
    /// <remarks>If set, the value True results in <see cref="Visibility.Collapsed"/>, and false in <see cref="Visibility.Visible"/>.</remarks>
    public bool Inverse { get; set; }

    /// <summary>
    /// Converts a source value to the target type.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameter"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    protected override bool Convert(string? value, object? parameter, string? language)
    {
        return Inverse ? !string.IsNullOrWhiteSpace(value) : string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Converts a target value back to the source type.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="parameter"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    protected override string ConvertBack(bool value, object? parameter, string? language)
    {
        return string.Empty;
    }
}

public class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null || parameter == null)
        {
            return Visibility.Collapsed;
        }

        return value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase) 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}


internal sealed class NullToTrueConverter : ValueConverter<object?, bool>
	{
		/// <summary>
		/// Determines whether an inverse conversion should take place.
		/// </summary>
		/// <remarks>If set, the value True results in <see cref="Visibility.Collapsed"/>, and false in <see cref="Visibility.Visible"/>.</remarks>
		public bool Inverse { get; set; }

		/// <summary>
		/// Converts a source value to the target type.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="parameter"></param>
		/// <param name="language"></param>
		/// <returns></returns>
		protected override bool Convert(object? value, object? parameter, string? language)
		{
			return Inverse ? value is not null : value is null;
		}

		/// <summary>
		/// Converts a target value back to the source type.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="parameter"></param>
		/// <param name="language"></param>
		/// <returns></returns>
		protected override object? ConvertBack(bool value, object? parameter, string? language)
		{
			return null;
		}
	}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count && count == 0)
        {
            return Visibility.Collapsed;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class NullBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Return true if the value is not null, false if it is null
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}


public class StoragePercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Check if value is not null and is of type StorageInfo, and TotalSpace is greater than zero
        if (value is StorageInfo storageInfo && storageInfo.TotalSpace > 0)
        {
            // Safely calculate the percentage of used space and return a valid double
            double percentage = (double)storageInfo.UsedSpace / storageInfo.TotalSpace * 100;
            return percentage;
        }

        // If the value is null or invalid, return 0 to prevent crashes
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
public class StorageInfoTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Check if the value is not null and is of type StorageInfo
        if (value is StorageInfo storageInfo)
        {
            // Convert the long values to GB for display purposes
            double freeSpaceGB = storageInfo.FreeSpace / 1_073_741_824.0;
            double totalSpaceGB = storageInfo.TotalSpace / 1_073_741_824.0;

            // Format and return the storage info text
            return $"{freeSpaceGB:F2} GB free of {totalSpaceGB:F2} GB";
        }

        // Return a fallback message if the value is null or invalid
        return "Storage information not available";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class NotificationFilterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is NotificationFilter filter)
        {
            return filter switch
            {
                NotificationFilter.ToastFeed => "NotificationFilterToastFeed/Content".GetLocalizedResource(),
                NotificationFilter.Feed => "NotificationFilterFeed/Content".GetLocalizedResource(),
                NotificationFilter.Disabled => "NotificationFilterDisabled/Content".GetLocalizedResource(),
                _ => ""
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string strValue)
        {
            if (strValue == "NotificationFilterToastFeed/Content".GetLocalizedResource())
                return NotificationFilter.ToastFeed;
            if (strValue == "NotificationFilterFeed/Content".GetLocalizedResource())
                return NotificationFilter.Feed;
            if (strValue == "NotificationFilterDisabled/Content".GetLocalizedResource())
                return NotificationFilter.Disabled;
        }
        return NotificationFilter.Disabled;
    }
}

public class NotificationLaunchPreferenceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is NotificationLaunchPreference preference)
        {
            return preference == NotificationLaunchPreference.OpenInRemoteDevice;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isChecked)
        {
            return isChecked ? NotificationLaunchPreference.OpenInRemoteDevice : NotificationLaunchPreference.Nothing;
        }
        return NotificationLaunchPreference.Nothing;
    }
}

public class BooleanToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isPinned)
        {
            return isPinned ? 100 : 0;
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class PinConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isPinned && isPinned)
        {
            return "Unpin".GetLocalizedResource();
        }
        return "Pin".GetLocalizedResource();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class PinIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool isPinned ? (isPinned ? "\uE77A" : "\uE718") : "\uE718";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class RingerModeToIconConverter : IValueConverter
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

public class MessageTypeToAlignmentConverter : IValueConverter
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

public class UnixTimestampConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long unixTimestampMs)
        {
            // Convert Unix timestamp (milliseconds) to DateTime
            DateTime dateTime = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestampMs).LocalDateTime;
            
            // Format based on how recent the message is
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

public class IndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int intValue)
        {
            // Convert from 1-based SIM ID to 0-based index
            return intValue - 1;
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is int intValue)
        {
            // Convert from 0-based index to 1-based SIM ID
            return intValue + 1;
        }
        return 1;
    }
}

public class GreaterThanOneToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count && count > 1)
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class SubscriptionToIconConverter : IValueConverter
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



