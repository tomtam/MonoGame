// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Linq;

namespace Microsoft.Xna.Framework.Content.Pipeline
{
    /// <summary>
    /// Defines a target platform supported by the content pipeline.
    /// </summary>
    public abstract class TargetPlatform : Enumeration<TargetPlatform>
    {
        protected TargetPlatform(string name, int value, char identifier)
            : base(name, value)
        {
            Identifier = identifier;
        }
        
        /// <summary>
        /// The unique platform identifier written to the XNB header.
        /// </summary>
        public char Identifier { get; private set; }

        /// <summary>
        /// Returns true if the platform name matches. 
        /// </summary>
        /// <param name="name">A valid platform name.</param>
        /// <remarks>If the platform name is invalid this will throw an ArgumentException.</remarks>
        public bool IsPlatform(string name)
        {
            var platform = All.FirstOrDefault(item => item.Name == name);
            if (platform == null)
                throw new ArgumentException("Invalid platform name '" + name + "'.");
            return ReferenceEquals(this, platform);
        }

        /// <summary>
        /// Returns a platform from a valid platform name.
        /// </summary>
        /// <param name="name">A valid platform name.</param>
        /// <remarks>If the platform name is invalid this will throw an ArgumentException.</remarks>
        public static TargetPlatform GetPlatform(string name)
        {
            var platform = All.FirstOrDefault(item => item.Name == name);
            if (platform == null)
                throw new ArgumentException("Invalid platform name '" + name + "'.");
            return platform;
        }

        /// <summary>
        /// All desktop versions of Windows using DirectX.
        /// </summary>
        class Windows : TargetPlatform { public Windows() : base("Windows", 0, 'w') { } }

        /// <summary>
        /// Windows Phone
        /// </summary>
        class WindowsPhone : TargetPlatform { public WindowsPhone() : base("WindowsPhone", 2, 'm') { } }

        /// <summary>
        /// Apple iOS-based devices (iPod Touch, iPhone, iPad)
        /// (MonoGame)
        /// </summary>
        class iOS : TargetPlatform { public iOS() : base("iOS", 3, 'i') { } }

        /// <summary>
        /// Android-based devices
        /// (MonoGame)
        /// </summary>
        class Android : TargetPlatform { public Android() : base("Android", 4, 'a') { } }

        /// <summary>
        /// All desktop versions using OpenGL.
        /// (MonoGame)
        /// </summary>
        class DesktopGL : TargetPlatform { public DesktopGL() : base("DesktopGL", 5, 'd') { } }

        /// <summary>
        /// Apple Mac OSX-based devices (iMac, MacBook, MacBook Air, etc)
        /// (MonoGame)
        /// </summary>
        class MacOSX : TargetPlatform { public MacOSX() : base("MacOSX", 6, 'X') { } }

        /// <summary>
        /// Windows Store App
        /// (MonoGame)
        /// </summary>
        class WindowsStoreApp : TargetPlatform { public WindowsStoreApp() : base("WindowsStoreApp", 7, 'W') { } }

        /// <summary>
        /// Google Chrome Native Client
        /// (MonoGame)
        /// </summary>
        class NativeClient : TargetPlatform { public NativeClient() : base("NativeClient", 8, 'n') { } }

        /// <summary>
        /// Sony PlayStation Mobile (PS Vita)
        /// (MonoGame)
        /// </summary>
        [Obsolete("PlayStation Mobile is no longer supported")]
        class PlayStationMobile : TargetPlatform { public PlayStationMobile() : base("PlayStationMobile", 9, 'p') { } }

        /// <summary>
        /// Windows Phone 8
        /// (MonoGame)
        /// </summary>
        class WindowsPhone8 : TargetPlatform { public WindowsPhone8() : base("WindowsPhone8", 10, 'M') { } }

        /// <summary>
        /// Raspberry Pi
        /// (MonoGame)
        /// </summary>
        class RaspberryPi : TargetPlatform { public RaspberryPi() : base("RaspberryPi", 11, 'r') { } }
    }
}
