using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework.Content.Pipeline;

namespace MonoGame.Tools.Pipeline
{
    class TargetPlatformTypeConverter : TypeConverter
    {
        private readonly StandardValuesCollection _values;

        public TargetPlatformTypeConverter()
        {
            var values = TargetPlatform.All.Select(p => p.Name).ToArray();
            Array.Sort(values);
            _values = new StandardValuesCollection(values);
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return _values;
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || sourceType == typeof(TargetPlatform);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value != null)
            {
                if (value is TargetPlatform)
                    return value;

                if (value is string)
                    return TargetPlatform.GetPlatform(value as string);
            }

            return base.ConvertFrom(context, culture, value);
        }


        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value != null)
            {
                if (value is string)
                    return value;

                return (value as TargetPlatform).Name;
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
