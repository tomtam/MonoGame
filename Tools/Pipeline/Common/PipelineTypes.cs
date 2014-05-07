﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using MonoGame.Framework.Content.Pipeline.Builder;

namespace MonoGame.Tools.Pipeline
{
    public class ImporterTypeDescription
    {        
        public string TypeName;
        public string DisplayName;
        public string DefaultProcessor;        
        public IEnumerable<string> FileExtensions;
        public Type OutputType;

        public override string ToString()
        {
            return TypeName;
        }
    };

    public class ProcessorTypeDescription
    {
        #region Supporting Types 

        public struct Property
        {
            public string Name;
            public Type Type;
            public object DefaultValue;

            public override string ToString()
            {
                return Name;
            }
        }

        public class ProcessorPropertyCollection : IEnumerable<Property>
        {
            private readonly Property[] _properties;

            public ProcessorPropertyCollection(IEnumerable<Property> properties)
            {
                _properties = properties.ToArray();
            }
 
            public Property this[int index]
            {
                get
                {
                    return _properties[index];
                }
                set
                {
                    _properties[index] = value;
                }
            }

            public Property this[string name]
            {
                get
                {
                    foreach (var p in _properties)
                    {
                        if (p.Name.Equals(name))
                            return p;
                    }

                    throw new IndexOutOfRangeException();
                }    
            
                set
                {
                    for (var i = 0; i < _properties.Length; i++)
                    {
                        var p = _properties[i];
                        if (p.Name.Equals(name))
                        {
                            _properties[i] = value;
                            return;
                        }

                    }

                    throw new IndexOutOfRangeException();
                }
            }

            public bool Contains(string name)
            {
                return _properties.Any(e => e.Name.Equals(name));
            }
            public IEnumerator<Property> GetEnumerator()
            {
                return _properties.AsEnumerable().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _properties.GetEnumerator();
            }
        }

        #endregion
        
        public string TypeName;
        public string DisplayName;
        public ProcessorPropertyCollection Properties;
        public Type InputType;

        public override string ToString()
        {
            return TypeName;
        }
    };

    internal class PipelineTypes
    {
        [DebuggerDisplay("ImporterInfo: {Type.Name}")]
        private struct ImporterInfo
        {
            public ContentImporterAttribute Attribute;
            public Type Type;
        }

        [DebuggerDisplay("ProcessorInfo: {Type.Name}")]
        private struct ProcessorInfo
        {
            public ContentProcessorAttribute Attribute;
            public Type Type;
        }

        private static List<ImporterInfo> _importers;
        private static List<ProcessorInfo> _processors;

        public static ImporterTypeDescription[] Importers { get; private set; }
        public static ProcessorTypeDescription[] Processors { get; private set; }

        public static ImporterTypeDescription NullImporter { get; private set; }
        public static ProcessorTypeDescription NullProcessor { get; private set; }

        public static ImporterTypeDescription MissingImporter { get; private set; }
        public static ProcessorTypeDescription MissingProcessor { get; private set; }

        private static readonly Dictionary<string, string> _oldNameRemap = new Dictionary<string, string>()
            {
                { "MGMaterialProcessor", "MaterialProcessor" },
                { "MGSongProcessor", "SongProcessor" },
                { "MGSoundEffectProcessor", "SoundEffectProcessor" },
                { "MGSpriteFontDescriptionProcessor", "FontDescriptionProcessor" },
                { "MGSpriteFontTextureProcessor", "FontTextureProcessor" },
                { "MGTextureProcessor", "TextureProcessor" },
            };

        private static string RemapOldNames(string name)
        {
            if (_oldNameRemap.ContainsKey(name))
                return _oldNameRemap[name];

            return name;
        }

        static PipelineTypes()
        {
            MissingImporter = new ImporterTypeDescription()
                {
                    DisplayName = "Invalid / Missing Importer",
                };

            MissingProcessor = new ProcessorTypeDescription()
                {
                    DisplayName = "Invalid / Missing Processor",
                    Properties = new ProcessorTypeDescription.ProcessorPropertyCollection(new ProcessorTypeDescription.Property[0]),
                };

            NullImporter = new ImporterTypeDescription()
            {
                DisplayName = "",
            };

            NullProcessor = new ProcessorTypeDescription()
            {
                DisplayName = "",
                Properties = new ProcessorTypeDescription.ProcessorPropertyCollection(new ProcessorTypeDescription.Property[0]),
            };
        }

        public static void Load(PipelineProject project, IUserOutput userOutput)
        {
            Unload();

            var assemblyPaths = new List<string>();

            var projectRoot = project.Location;

            foreach (var i in project.References)
            {
                var path = i;
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(projectRoot, path);

                if (string.IsNullOrEmpty(path))
                    throw new ArgumentException("assemblyFilePath cannot be null!");
                if (!Path.IsPathRooted(path))
                    throw new ArgumentException("assemblyFilePath must be absolute!");

                // Make sure we're not adding the same assembly twice.
                path = PathHelper.Normalize(path);
                if (!assemblyPaths.Contains(path))
                    assemblyPaths.Add(path);      
            }

            ResolveAssemblies(assemblyPaths, userOutput);

            var importerDescriptions = new ImporterTypeDescription[_importers.Count];
            var cur = 0;
            foreach (var item in _importers)
            {
                var outputType = item.Type.BaseType.GenericTypeArguments[0];
                var desc = new ImporterTypeDescription()
                    {
                        TypeName = item.Type.Name,
                        DisplayName = item.Attribute.DisplayName,
                        DefaultProcessor = item.Attribute.DefaultProcessor,                        
                        FileExtensions = item.Attribute.FileExtensions,   
                        OutputType = outputType,
                    };
                importerDescriptions[cur] = desc;
                cur++;
            }

            Importers = importerDescriptions;

            var processorDescriptions = new ProcessorTypeDescription[_processors.Count];

            cur = 0;
            foreach (var item in _processors)
            {
                var obj = Activator.CreateInstance(item.Type);
                var typeProperties = item.Type.GetRuntimeProperties();
                var properties = new List<ProcessorTypeDescription.Property>();
                foreach (var i in typeProperties)
                {
                    // TODO:
                    //p.GetCustomAttribute(typeof(ContentPipelineIgnore))

                    var p = new ProcessorTypeDescription.Property()
                        {
                            Name = i.Name,
                            Type = i.PropertyType,
                            DefaultValue = i.GetValue(obj),
                        };
                    properties.Add(p);
                }

                var inputType = (obj as IContentProcessor).InputType;
                var desc = new ProcessorTypeDescription()
                {
                    TypeName = item.Type.Name,
                    DisplayName = item.Attribute.DisplayName,
                    Properties = new ProcessorTypeDescription.ProcessorPropertyCollection(properties),
                    InputType = inputType,
                };
                if (string.IsNullOrEmpty(desc.DisplayName))
                    desc.DisplayName = desc.TypeName;

                processorDescriptions[cur] = desc;
                cur++;
            }

            Processors = processorDescriptions;            
        }

        public static void Unload()
        {            
            _importers = null;
            Importers = null;
         
            _processors = null;
            Processors = null;
        }        

        public static ImporterTypeDescription FindImporter(string name, string fileExtension)
        {
            if (!string.IsNullOrEmpty(name))
            {
                name = RemapOldNames(name);
                
                foreach (var i in Importers)
                {
                    if (i.TypeName.Equals(name))
                        return i;
                }

                foreach (var i in Importers)
                {
                    if (i.DisplayName.Equals(name))
                        return i;
                }

                //Debug.Fail(string.Format("Importer not found! name={0}, ext={1}", name, fileExtension));
                return null;
            }

            foreach (var i in Importers)
            {
                if (i.FileExtensions.Contains(fileExtension))
                    return i;
            }

            //Debug.Fail(string.Format("Importer not found! name={0}, ext={1}", name, fileExtension));
            return null;
        }

        public static ProcessorTypeDescription FindProcessor(string name, ImporterTypeDescription importer)
        {
            if (!string.IsNullOrEmpty(name))
            {
                name = RemapOldNames(name);

                foreach (var i in Processors)
                {
                    if (i.TypeName.Equals(name))
                        return i;
                }

                //Debug.Fail(string.Format("Processor not found! name={0}, importer={1}", name, importer));
                return null;
            }

            if (importer != null)
            {
                foreach (var i in Processors)
                {
                    if (i.TypeName.Equals(importer.DefaultProcessor))
                        return i;
                }
            }

            //Debug.Fail(string.Format("Processor not found! name={0}, importer={1}", name, importer));
            return null;
        }

        private static void ResolveAssemblies(IEnumerable<string> assemblyPaths, IUserOutput userOutput)
        {
            _importers = new List<ImporterInfo>();
            _processors = new List<ProcessorInfo>();
            
            var assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

            foreach (var asm in assemblies)
            {
#if SHIPPING
                try
#endif
                {
                    var types = asm.GetTypes();
                    ProcessTypes(types);
                }
#if SHIPPING
                catch (Exception e)
                {
                    // ??
                }
#endif
            }

            foreach (var path in assemblyPaths)
            {
                try
                {                    
                    var a = Assembly.LoadFrom(path);
                    var types = a.GetExportedTypes();
                    ProcessTypes(types);

                    userOutput.OutputAppend(string.Format("Loaded reference assembly '{0}'", path));  
                }
                catch
                {
                    userOutput.ShowError("Error", string.Format("Failed to load reference assembly'{0}'", path));                    
                }                
            }
        }

        private static void ProcessTypes(IEnumerable<Type> types)
        {
            foreach (var t in types)
            {
                if (t.IsAbstract)
                    continue;

                if (t.GetInterface(@"IContentImporter") == typeof(IContentImporter))
                {
                    var attributes = t.GetCustomAttributes(typeof(ContentImporterAttribute), false);
                    if (attributes.Length != 0)
                    {
                        var importerAttribute = attributes[0] as ContentImporterAttribute;
                        _importers.Add(new ImporterInfo { Attribute = importerAttribute, Type = t });
                    }
                    else
                    {
                        // If no attribute specify default one
                        var importerAttribute = new ContentImporterAttribute(".*");
                        importerAttribute.DefaultProcessor = "";
                        importerAttribute.DisplayName = t.Name;
                        _importers.Add(new ImporterInfo { Attribute = importerAttribute, Type = t });
                    }
                }
                else if (t.GetInterface(@"IContentProcessor") == typeof(IContentProcessor))
                {
                    var attributes = t.GetCustomAttributes(typeof(ContentProcessorAttribute), false);
                    if (attributes.Length != 0)
                    {
                        var processorAttribute = attributes[0] as ContentProcessorAttribute;
                        _processors.Add(new ProcessorInfo { Attribute = processorAttribute, Type = t });
                    }
                }
            }
        }
    }
}
