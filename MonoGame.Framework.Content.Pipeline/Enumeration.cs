// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;


namespace Microsoft.Xna.Framework.Content.Pipeline
{
    public class EnumerationBase : IComparable
    {
        private readonly int _value;

        private readonly string _name;

        protected EnumerationBase(string name, int value)
        {
            _value = value;
            _name = name;
        }

        public string Name
        {
            get { return _name; }
        }

        public int Value
        {
            get { return _value; }
        }

        public override string ToString()
        {
            return _name;
        }

        public override bool Equals(object obj)
        {
            var other = obj as EnumerationBase;
            if (other == null)
                return false;

            if (GetType() != other.GetType())
                return false;

            if (Value != other.Value)
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public int CompareTo(object obj)
        {
            return Value.CompareTo(((EnumerationBase)obj).Value);
        }
    }


    public class Enumeration<TType> : EnumerationBase
        where TType : EnumerationBase
    {
        private static readonly List<TType> _all;

        static Enumeration()
        {
            TypeDescriptor.AddAttributes(typeof(TType), new TypeConverterAttribute(typeof(StringConverter)));           
            _all = new List<TType>();

            var assembly = typeof(TType).Assembly;
            foreach (var t in assembly.GetTypes())
            {
                if (!t.IsAbstract && t.IsSubclassOf(typeof(TType)))
                {
                    var ttype = (TType)Activator.CreateInstance(t);
                    _all.Add(ttype);
                }
            }
        }

        protected Enumeration(string name, int value)
            : base(name, value)
        {
        }

        public static IEnumerable<TType> All
        {
            get { return _all; }
        }

        public static bool operator ==(Enumeration<TType> first, Enumeration<TType> second)
        {
            if (ReferenceEquals(first, null) || ReferenceEquals(second, null))
                return false;

            return first.Value == second.Value;
        }

        public static bool operator !=(Enumeration<TType> first, Enumeration<TType> second)
        {
            if (ReferenceEquals(first, null) || ReferenceEquals(second, null))
                return false;

            return first.Value != second.Value;
        }

        private class StringConverter : TypeConverter
        {
            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                if (value is string)
                {
                    var name = value as string;

                    foreach (var e in _all)
                    {
                        if (e.Name == name)
                            return e;
                    }
                }

                return base.ConvertFrom(context, culture, value);
            }
        }
    }
}
