// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.ComponentModel;


namespace MGCB
{    
    
    

    /// <summary>
    /// Adapted from this generic command line argument parser:
    /// http://blogs.msdn.com/b/shawnhar/archive/2012/04/20/a-reusable-reflection-based-command-line-parser.aspx     
    /// </summary>
    public class MGBuildParser
    {
        #region Supporting Types

        public class LineSource
        {
            public string File;
            public int Line;

            public override string ToString()
            {
                var fileStr = "[file:null]";
                if (!string.IsNullOrEmpty(File))
                    fileStr = string.Concat("[file:", File, "]");

                return string.Concat(fileStr, "[line:", Line, "]");
            }
        }

        public class Command
        {
            public LineSource Source;
            public string Text;

            public override string ToString()
            {
                return string.Concat(Source.ToString(), " ", Text);
            }
        }  

        public class Option
        {
            public CommandLineParameterAttribute Attribute;
            public MemberInfo Member;

            public Type DataType
            {
                get
                {
                    if (IsList(Member))
                    {
                        return ListElementType(Member);
                    }

                    if (Member is MethodInfo)
                    {
                        var method = Member as MethodInfo;
                        var parameters = method.GetParameters();

                        if (parameters.Length == 0)
                            return null;

                        return parameters[0].ParameterType;
                    }

                    if (Member is FieldInfo)
                    {
                        var field = Member as FieldInfo;
                        return field.FieldType;
                    }

                    if (Member is PropertyInfo)
                    {
                        var property = Member as PropertyInfo;
                        return property.PropertyType;
                    }

                    throw new Exception("Unhandled case");
                }
            }

            public bool HasValue()
            {
                if (Member is FieldInfo)
                    return (Member as FieldInfo).FieldType != typeof (bool);
                
                if (Member is PropertyInfo)
                    return (Member as PropertyInfo).PropertyType != typeof (bool);

                if (Member is MethodInfo)
                    return (Member as MethodInfo).GetParameters().Length != 0;

                throw new Exception("Unhandled case");
            }

            public void Set(object target, object value)
            {                
                if (IsList(Member))
                {                    
                    GetList(Member, target).Add(value);
                }
                else
                {
                    if (Member is MethodInfo)
                    {
                        var method = Member as MethodInfo;
                        var parameters = method.GetParameters();
                        if (parameters.Length == 0)
                            method.Invoke(target, null);
                        else
                            method.Invoke(target, new[] { value });
                    }
                    else if (Member is FieldInfo)
                    {
                        var field = Member as FieldInfo;
                        field.SetValue(target, value);
                    }
                    else
                    {
                        var property = Member as PropertyInfo;
                        property.SetValue(target, value, null);
                    }
                }
            }
        }

        public class PreprocessorProperty
        {
            public string Name;            
            public string CurrentValue;

            public PreprocessorProperty()
            {
                Name = string.Empty;
                CurrentValue = string.Empty;
            }
        }

        public class PreprocessorPropertyCollection
        {
            private readonly List<PreprocessorProperty> _properties;

            public PreprocessorPropertyCollection()
            {
                _properties = new List<PreprocessorProperty>();
            }

            public string this[string name]
            {
                get
                {
                    foreach (var i in _properties)
                    {
                        if (i.Name.Equals(name))
                            return i.CurrentValue;
                    }

                    return null;
                }

                set
                {
                    foreach (var i in _properties)
                    {
                        if (i.Name.Equals(name))
                        {
                            i.CurrentValue = value;
                            return;
                        }
                    }

                    var prop = new PreprocessorProperty()
                        {
                            Name = name,
                            CurrentValue = value,
                        };
                    _properties.Add(prop);
                }
            }
        }

        #endregion

        private readonly object _optionsObject;
        private readonly Queue<Option> _requiredOptions;
        private readonly Dictionary<string, Option> _optionalOptions;
        private readonly List<string> _requiredUsageHelp;

        public readonly PreprocessorPropertyCollection _properties;

        public delegate void ErrorCallback(string msg, object[] args);
        public event ErrorCallback OnError;

        public MGBuildParser(object optionsObject)
        {
            _optionsObject = optionsObject;
            _requiredOptions = new Queue<Option>();
            _optionalOptions = new Dictionary<string, Option>();
            _requiredUsageHelp = new List<string>();

            _properties = new PreprocessorPropertyCollection();

            // Reflect to find what commandline options are available...

            // Fields
            foreach (var field in optionsObject.GetType().GetFields())
            {
                var attr = GetAttribute<CommandLineParameterAttribute>(field);
                if (attr == null)
                    continue;

                CheckReservedPrefixes(attr.Name);
                
                var option = new Option()
                {
                    Attribute = attr,
                    Member = field,
                };

                if (attr.Required)
                {
                    // Record a required option.                    
                    _requiredOptions.Enqueue(option);

                    _requiredUsageHelp.Add(string.Format("<{0}>", attr.Name));
                }
                else
                {
                    // Record an optional option.
                    _optionalOptions.Add(attr.Name.ToLowerInvariant(), option);
                }
            }

            // Properties
            foreach (var property in optionsObject.GetType().GetProperties())
            {
                var attr = GetAttribute<CommandLineParameterAttribute>(property);
                if (attr == null)
                    continue;

                CheckReservedPrefixes(attr.Name);

                var option = new Option()
                {
                    Attribute = attr,
                    Member = property,
                };

                if (attr.Required)
                {
                    // Record a required option.
                    _requiredOptions.Enqueue(option);

                    _requiredUsageHelp.Add(string.Format("<{0}>", attr.Name));
                }
                else
                {
                    // Record an optional option.
                    _optionalOptions.Add(attr.Name.ToLowerInvariant(), option);
                }
            }

            // Methods
            foreach (var method in optionsObject.GetType().GetMethods())
            {
                var attr = GetAttribute<CommandLineParameterAttribute>(method);
                if (attr == null)
                    continue;

                CheckReservedPrefixes(attr.Name);

                // Only accept methods that take less than 1 parameter.
                if (method.GetParameters().Length > 1)
                    throw new NotSupportedException("Methods must have one or zero parameters.");

                var option = new Option()
                {
                    Attribute = attr,
                    Member = method,
                };

                if (attr.Required)
                {
                    // Record a required option.
                    _requiredOptions.Enqueue(option);

                    _requiredUsageHelp.Add(string.Format("<{0}>", attr.Name));
                }
                else
                {
                    // Record an optional option.
                    _optionalOptions.Add(attr.Name.ToLowerInvariant(), option);
                }
            }
        }        

        public bool Parse(IEnumerable<string> args)
        {
            var commands = Preprocess(args);

            var success = true;            
            foreach (var cmd in commands)
            {
                if (!ParseCommand(cmd))
                {
                    success = false;
                    break;
                }
            }

            var missingRequiredOption = _requiredOptions.FirstOrDefault(field => !IsList(field.Member) || GetList(field.Member, _optionalOptions).Count == 0);
            if (missingRequiredOption != null)
            {
                ShowError("Missing argument '{0}'", missingRequiredOption.Attribute.Name);
                return false;
            }

            return success;
        }

        private IEnumerable<Command> Preprocess(IEnumerable<string> args)
        {
            var output = new List<Command>();
            var lines = new List<string>(args);
            var ifstack = new Stack<Tuple<string, string>>();
            var sourceStack = new Stack<LineSource>();                        
            var currentSource = new LineSource();            

            while (lines.Count > 0)
            {            
                var arg = lines[0];
                lines.RemoveAt(0);                

                if (arg.StartsWith("# Begin:"))
                {                    
                    sourceStack.Push(currentSource);

                    var file = arg.Substring(8);

                    currentSource = new LineSource()
                        {
                            File = file,
                            Line = 0,
                        };

                    continue;
                }

                if (arg.StartsWith("# End:"))
                {
                    currentSource = sourceStack.Pop();

                    continue;
                }

                if (arg.StartsWith("$endif"))
                {
                    ifstack.Pop();

                    continue;
                }
                
                if (ifstack.Count > 0)
                {
                    var skip = false;
                    foreach (var i in ifstack)
                    {
                        var val = _properties[i.Item1];
                        if (!(i.Item2).Equals(val))
                        {
                            skip = true;
                            break;
                        }
                    }

                    if (skip)
                        continue;
                }

                if (arg.StartsWith("$set"))
                {
                    var words = arg.Substring(5).Split('=');
                    var name = words[0];
                    var value = words[1];

                    _properties[name] = value;

                    continue;
                }

                if (arg.StartsWith("$if"))
                {
                    if (sourceStack.Count == 0)
                        throw new Exception("$if is invalid outside of a response file.");

                    var words = arg.Substring(4).Split('=');
                    var name = words[0];
                    var value = words[1];

                    var condition = new Tuple<string, string>(name, value);
                    ifstack.Push(condition);
                    
                    continue;
                }

                if (arg.StartsWith("/@"))
                {
                    var file = arg.Substring(3);
                    var commands = File.ReadAllLines(file);
                    var offset = 0;
                    lines.Insert(0, string.Concat("# Begin:", file));
                    offset++;

                    for (var j = 0; j < commands.Length; j++)
                    {
                        var line = commands[j];
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line))
                            continue;
                        if (line.StartsWith("#"))
                            continue;

                        lines.Insert(offset, line);
                        offset++;
                    }

                    lines.Insert(offset, string.Concat("# End:", file));

                    continue;
                }

                var cmd = new Command()
                    {
                        Source = new LineSource()
                            {
                                File = currentSource.File,
                                Line = currentSource.Line,
                            },
                        Text = arg,
                    };
                output.Add(cmd);

                currentSource.Line++;
            }

            return output.ToArray();
        }

        private bool ParseCommand(Command cmd)
        {
            var txt = cmd.Text;

            if (txt.StartsWith("/"))
            {
                // After the first escaped argument we can no
                // longer read non-escaped arguments.
                if (_requiredOptions.Count > 0)
                    return false;

                // Parse an optional argument.
                char[] separators = {':'};

                var split = txt.Substring(1).Split(separators, 2, StringSplitOptions.None);

                var name = split[0];
                var value = (split.Length > 1) ? split[1] : "true";

                Option option;
                if (!_optionalOptions.TryGetValue(name.ToLowerInvariant(), out option))
                {
                    ShowError("Unknown option '{0}'", name);
                    return false;
                }

                return SetOption(option, cmd.Source, value);
            }

            if (_requiredOptions.Count > 0)
            {
                // Parse the next non escaped argument.
                var option = _requiredOptions.Peek();

                if (!IsList(option.Member))
                    _requiredOptions.Dequeue();

                return SetOption(option, cmd.Source, cmd.Text);
            }

            ShowError("Too many arguments");
            return false;
        }


        bool SetOption(Option option, LineSource source, string valueStr)
        {
            try
            {
                var dataType = option.DataType;                

                if (dataType == typeof(string) && option.Attribute.IsPath)
                {                   
                    if (!Path.IsPathRooted(valueStr))
                    {
                        var rootDir = Path.GetDirectoryName(source.File);
                        valueStr = Path.Combine(rootDir, valueStr);
                    }
                }

                var value = ChangeType(valueStr, dataType);
                option.Set(_optionsObject, value);

                return true;
            }
            catch
            {
                ShowError("Invalid value '{0}' for option '{1}'", valueStr, option.Attribute.Name);
                return false;
            }
        }

        static readonly string[] ReservedPrefixes = new[]
            {   
                "$",
                "/",                
                "#",                
            };

        static void CheckReservedPrefixes(string str)
        {
            foreach (var i in ReservedPrefixes)
            {
                if (str.StartsWith(i))
                    throw new Exception(string.Format("'{0}' is a reserved prefix and cannot be used at the start of an argument name.", i));
            }
        }

        static object ChangeType(string value, Type type)
        {
            var converter = TypeDescriptor.GetConverter(type);
            return converter.ConvertFromInvariantString(value);
        }


        static bool IsList(MemberInfo member)
        {
            if (member is MethodInfo)
                return false;

            if (member is FieldInfo)
                return typeof(IList).IsAssignableFrom((member as FieldInfo).FieldType);
            
            return typeof(IList).IsAssignableFrom((member as PropertyInfo).PropertyType);
        }


        static IList GetList(MemberInfo member, object target)
        {
            if (member is PropertyInfo)
                return (IList)(member as PropertyInfo).GetValue(target, null);

            if (member is FieldInfo)
                return (IList)(member as FieldInfo).GetValue(target);

            throw new Exception();
        }


        static Type ListElementType(MemberInfo member)
        {
            if (member is FieldInfo)
            {
                var field = member as FieldInfo;
                var interfaces = from i in field.FieldType.GetInterfaces()
                                 where i.IsGenericType && i.GetGenericTypeDefinition() == typeof (IEnumerable<>)
                                 select i;

                return interfaces.First().GetGenericArguments()[0];
            }

            if (member is PropertyInfo)
            {
                var property = member as PropertyInfo;
                var interfaces = from i in property.PropertyType.GetInterfaces()
                                 where i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                                 select i;

                return interfaces.First().GetGenericArguments()[0];
            }

            throw new ArgumentException("Only FieldInfo and PropertyInfo are valid arguments.", "member");
        }

        public string Title { get; set; }

        public void ShowUsage()
        {
            ShowError(null);
        }

        public void ShowError(string message, params object[] args)
        {
            if (!string.IsNullOrEmpty(message) && OnError != null)
            {
                OnError(message, args);
                return;
            }

            var name = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().ProcessName);

            if (!string.IsNullOrEmpty(Title))
            {
                Console.Error.WriteLine(Title);
                Console.Error.WriteLine();
            }

            if (!string.IsNullOrEmpty(message))
            {
                Console.Error.WriteLine(message, args);
                Console.Error.WriteLine();
            }

            Console.Error.WriteLine("Usage: {0} {1}{2}", 
                name, 
                string.Join(" ", _requiredUsageHelp), 
                _optionalOptions.Count > 0 ? " <Options>" : string.Empty);

            if (_optionalOptions.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Options:\n");

                foreach (var pair in _optionalOptions)
                {
                    var option = pair.Value as Option;

                    var attr = option.Attribute;

                    var hasValue = option.HasValue();                    

                    if (hasValue)
                        Console.Error.WriteLine("  /{0}:<{1}>\n    {2}\n", attr.Name, attr.ValueName, attr.Description);
                    else
                        Console.Error.WriteLine("  /{0}\n    {1}\n", attr.Name, attr.Description);
                }
            }
        }


        static T GetAttribute<T>(ICustomAttributeProvider provider) where T : Attribute
        {
            return provider.GetCustomAttributes(typeof(T), false).OfType<T>().FirstOrDefault();
        }
    }

    // Used on an optionsObject field or method to rename the corresponding commandline option.
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class CommandLineParameterAttribute : Attribute
    {
        public CommandLineParameterAttribute()
        {
            ValueName = "value";
        }

        public string Name { get; set; }

        public bool Required { get; set; }

        public string ValueName { get; set; }

        public string Description { get; set; }

        public bool IsPath { get; set; }
    }
}
