﻿// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MGCB;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;
using PathHelper = MonoGame.Framework.Content.Pipeline.Builder.PathHelper;

namespace MonoGame.Tools.Pipeline
{
    /// <summary>
    /// The pipline project and helper methods.
    /// </summary>
    /// <remarks>
    /// NOTE: This class should never have any dependancy on the 
    /// controller or view... it is only the data "model".
    /// </remarks>
    class PipelineProject : IProjectItem
    {
        private readonly List<ContentItem> _content = new List<ContentItem>();

        public ReadOnlyCollection<ContentItem> ContentItems { get; private set; }

        public string FilePath { get; set; }

        [CommandLineParameter(
            Name = "outputDir",
            ValueName = "directoryPath",
            Description = "The directory where all content is written.")]
        public string OutputDir = string.Empty;

        [CommandLineParameter(
            Name = "intermediateDir",
            ValueName = "directoryPath",
            Description = "The directory where all intermediate files are written.")]
        public string IntermediateDir = string.Empty;

        [CommandLineParameter(
            Name = "reference",
            ValueName = "assemblyNameOrFile",
            Description = "Adds an assembly reference for resolving content importers, processors, and writers.")]
        public readonly List<string> References = new List<string>();

        [CommandLineParameter(
            Name = "platform",
            ValueName = "targetPlatform",
            Description = "Set the target platform for this build.  Defaults to Windows.")]
        public TargetPlatform Platform;

        [CommandLineParameter(
            Name = "profile",
            ValueName = "graphicsProfile",
            Description = "Set the target graphics profile for this build.  Defaults to HiDef.")]
        public GraphicsProfile Profile;

        [CommandLineParameter(
            Name = "config",
            ValueName = "string",
            Description = "The optional build config string from the build system.")]
        public string Config = string.Empty;

        [CommandLineParameter(
            Name = "importer",
            ValueName = "className",
            Description = "Defines the class name of the content importer for reading source content.")]
        public string Importer;

        private string _processor;

        [CommandLineParameter(
            Name = "processor",
            ValueName = "className",
            Description = "Defines the class name of the content processor for processing imported content.")]
        public string Processor
        {
            get { return _processor; }
            set
            {
                _processor = value;
                _processorParams.Clear();
            }
        }

        private readonly OpaqueDataDictionary _processorParams = new OpaqueDataDictionary();

        [CommandLineParameter(
            Name = "processorParam",
            ValueName = "name=value",
            Description = "Defines a parameter name and value to set on a content processor.")]
        public void AddProcessorParam(string nameAndValue)
        {
            var keyAndValue = nameAndValue.Split('=');
            if (keyAndValue.Length != 2)
            {
                // Do we error out or something?
                return;
            }

            _processorParams.Remove(keyAndValue[0]);
            _processorParams.Add(keyAndValue[0], keyAndValue[1]);
        }

        [CommandLineParameter(
            Name = "build",
            ValueName = "sourceFile",
            Description = "Build the content source file using the previously set switches and options.")]
        public void OnBuild(string sourceFile)
        {
            // Make sure the source file is relative to the project.
            var projectDir = System.IO.Path.GetDirectoryName(FilePath);
            sourceFile = PathHelper.GetRelativePath(projectDir, sourceFile);

            // Remove duplicates... keep this new one.
            var previous = _content.FindIndex(e => string.Equals(e.SourceFile, sourceFile, StringComparison.InvariantCultureIgnoreCase));
            if (previous != -1)
                _content.RemoveAt(previous);

            // Create the item for processing later.
            var item = new ContentItem
            {
                SourceFile = sourceFile,
                ImporterName = Importer,
                ProcessorName = Processor,
                ProcessorParams = new OpaqueDataDictionary()
            };
            _content.Add(item);

            // Copy the current processor parameters blind as we
            // will validate and remove invalid parameters during
            // the build process later.
            foreach (var pair in _processorParams)
                item.ProcessorParams.Add(pair.Key, pair.Value);
        }

        public bool IsDirty { get; set; }

        public PipelineProject()
        {
            ContentItems = new ReadOnlyCollection<ContentItem>(_content);
        }

        public void Attach(IProjectObserver observer)
        {            
        }

        public void NewProject()
        {
            _content.Clear();
            References.Clear();
            OutputDir = null;
            IntermediateDir = null;
            Config = null;
            Importer = null;
            Platform = TargetPlatform.Windows;
            Profile = GraphicsProfile.HiDef;
            Processor = null;
            FilePath = null;
            IsDirty = false;
        }

        public void OpenProject(string projectFilePath)
        {
            _content.Clear();

            // Store the file name for saving later.
            FilePath = projectFilePath;

            var parser = new CommandLineParser(this);
            parser.Title = "Pipeline";

            var commands = File.ReadAllLines(projectFilePath).
                            Select(x => x.Trim()).
                            Where(x => !string.IsNullOrEmpty(x) && !x.StartsWith("#")).
                            ToArray();

            parser.ParseCommandLine(commands);

            // We're not dirty as we just loaded.
            IsDirty = false;
        }

        public void SaveProject()
        {
            const string lineFormat = "/{0}:{1}";
            const string processorParamFormat = "{0}={1}";
            const string commentFormat = "\n#---------------------- {0} ----------------------#\n";
            string line;
            var parameterLines = new List<string>();

            using (var io = File.CreateText(FilePath))
            {
                line = string.Format(commentFormat, "Global Properties");
                io.WriteLine(line);

                line = string.Format(lineFormat, "outputDir", OutputDir);
                io.WriteLine(line);

                line = string.Format(lineFormat, "intermediateDir", IntermediateDir);
                io.WriteLine(line);

                line = string.Format(lineFormat, "platform", Platform);
                io.WriteLine(line);

                line = string.Format(lineFormat, "config", Config);
                io.WriteLine(line);

                line = string.Format(commentFormat, "References");
                io.WriteLine(line);

                foreach (var i in References)
                {
                    line = string.Format(lineFormat, "reference", i);
                    io.WriteLine(line);
                }

                line = string.Format(commentFormat, "Content");
                io.WriteLine(line);

                foreach (var i in ContentItems)
                {
                    if (!i.Importer.FileExtensions.Contains(System.IO.Path.GetExtension(i.SourceFile)))
                    {
                        line = string.Format(lineFormat, "importer", i.ImporterName);
                        io.WriteLine(line);   
                    }

                    // Collect lines for each non-default-value processor parameter
                    // but do not write them yet.
                    parameterLines.Clear();
                    foreach (var j in i.ProcessorParams)
                    {
                        var defaultValue = i.Processor.Properties[j.Key].DefaultValue;
                        if (j.Value == null || j.Value == defaultValue)
                            continue;

                        line = string.Format(lineFormat, "processorParam", string.Format(processorParamFormat, j.Key, j.Value));
                        parameterLines.Add(line);
                    }

                    // If there were any non-default-value processor parameters
                    // or, if the processor itself is not the default processor for this content's importer
                    // then we write out the processor command line and any (non default value) processor parameters.
                    if (parameterLines.Count > 0 || !i.Processor.TypeName.Equals(i.Importer.DefaultProcessor))
                    {
                        line = string.Format(lineFormat, "processor", i.ProcessorName);
                        io.WriteLine(line);

                        foreach (var ln in parameterLines)
                            io.WriteLine(ln);
                    }

                    line = string.Format(lineFormat, "build", i.SourceFile);
                    io.WriteLine(line);
                    io.WriteLine();
                }
            }
        }

        public void CloseProject()
        {
            _content.Clear();
        }

#region IPipelineItem

        public string Name 
        { 
            get
            {
                return System.IO.Path.GetFileNameWithoutExtension(FilePath);
            }
        }

        public string Location
        {
            get { return string.Empty; }
        }

        public string Icon { get; set; }

#endregion
    }
}