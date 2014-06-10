// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using FolderSelect;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGame.Tools.Pipeline
{
    /// <summary>
    /// Wraps a PipelineProject object, defining its appearance within the windows specific IView (MainView).
    /// </summary>
    internal class PipelineProjectProxy : IProjectItem
    {
        private readonly PipelineProject _project;

        [DisplayName("\tPlatformsDefined")]
        [Category("Defines")]
        [Description("Platform names for which this project and its items have different settings.")]
        [Editor(typeof(StringListEditor), typeof(UITypeEditor))]
        public List<string> PlatformsDefined
        {
            get { return _project.PlatformsDefined; }
            set { _project.PlatformsDefined = value; }
        }

        [DisplayName("\tConfigsDefined")]
        [Category("Defines")]
        [Description("Configuration names for which this project and its items have different settings.")]
        [Editor(typeof(StringListEditor), typeof(UITypeEditor))]
        public List<string> ConfigsDefined
        {
            get { return _project.ConfigsDefined; }
            set { _project.ConfigsDefined = value; }
        }

        [Category("Settings")]
        [DisplayName("Output Folder")]
        [Description("The folder where the final build content is placed.")]
        [Editor(typeof(FolderSelectEditor), typeof(UITypeEditor))]
        public string OutputDir
        {
            get { return _project.OutputDir; }
            set
            {
                _project.OutputDir = Util.GetRelativePath(value, _project.Location);
            }
        }

        [Category("Settings")]
        [DisplayName("Intermediate Folder")]
        [Description("The folder where intermediate files are placed when building content.")]
        [Editor(typeof(FolderSelectEditor), typeof(UITypeEditor))]
        public string IntermediateDir
        {
            get { return _project.IntermediateDir; }
            set
            {       
                _project.IntermediateDir = Util.GetRelativePath(value, _project.Location);
            }
        }

        [Category("Settings")]
        [Editor(typeof (StringListEditor), typeof (UITypeEditor))]
        public List<string> References
        {
            get { return _project.References; }
            set { _project.References = value; }
        }

        [Category("Settings")]
        [Description("The platform to target when building content.")]
        public TargetPlatform Platform
        {
            get { return _project.Platform; }
            set { _project.Platform = value; }
        }

        [Category("Settings")]
        [Description("The config to target when building content.")]
        public string Config
        {
            get { return _project.Config; }
            set { _project.Config = value; }
        }

        [Category("Settings")]
        [Description("The graphics profile to target when building content.")]
        public GraphicsProfile Profile
        {
            get { return _project.Profile; }
            set { _project.Profile = value; }
        }        

        [Category("Statistics")]
        [DisplayName("Total Items")]
        [Description("The total amount of content items in the project.")]
        public int TotalItems
        {
            get
            {
                return _project.ContentItems.Count;
            }
        }

        #region IPipelineItem

        [Browsable(false)]
        public string OriginalPath
        {
            get { return _project.OriginalPath; }
        }

        [Category("Common")]
        [Description("The name of this project.")]
        public string Name
        {
            get
            {
                return _project.Name;                
            }
        }

        [Category("Common")]
        [Description("The folder where this project file is located.")]
        public string Location
        {
            get
            {
                return _project.Location;                
            }
        }

        [Browsable(false)]
        public string Icon
        {
            get { return _project.Icon; }
            set { _project.Icon = value; }
        }

        #endregion

        public PipelineProjectProxy(PipelineProject project)
        {
            _project = project;
        }
    }
}