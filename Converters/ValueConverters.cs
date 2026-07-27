using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using MilkWala.Models;

namespace MilkWala.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DeliveryStatus status)
            {
                return status switch
                {
                    DeliveryStatus.Delivered => Color.FromArgb("#10B981"), // Emerald Green
                    DeliveryStatus.Skipped => Color.FromArgb("#F59E0B"),   // Amber
                    DeliveryStatus.Holiday => Color.FromArgb("#6B7280"),   // Cool Gray
                    DeliveryStatus.Extra => Color.FromArgb("#8B5CF6"),     // Purple
                    DeliveryStatus.Missed => Color.FromArgb("#EF4444"),    // Red
                    DeliveryStatus.Pending => Color.FromArgb("#3B82F6"),   // Blue
                    _ => Color.FromArgb("#6B7280")
                };
            }
            return Color.FromArgb("#6B7280");
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CurrencyFormatConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal amount)
            {
                return $"₹{amount:N2}";
            }
            if (value is double dblAmount)
            {
                return $"₹{dblAmount:N2}";
            }
            return "₹0.00";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }
    }

    public class StringHasTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
