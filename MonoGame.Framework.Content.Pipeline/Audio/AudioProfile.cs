// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Linq;


namespace Microsoft.Xna.Framework.Content.Pipeline.Audio
{
    public abstract class AudioProfile
    {
        private static readonly List<AudioProfile> Profiles = new List<AudioProfile>();

        static AudioProfile()
        {
            // TODO: Eventually we should be scanning all loaded 
            // content pipeline assemblies for these to allow for
            // plugin platform support.

            var assembly = typeof(AudioProfile).Assembly;
            foreach (var t in assembly.GetTypes())
            {
                if (!t.IsAbstract && t.IsSubclassOf(typeof(AudioProfile)))
                {
                    var profile = (AudioProfile)Activator.CreateInstance(t);
                    Profiles.Add(profile);
                }
            }
        }

        /// <summary>
        /// Find the profile for this target platform.
        /// </summary>
        /// <param name="platform">The platform target for audio.</param>
        /// <returns></returns>
        public static AudioProfile ForPlatform(TargetPlatform platform)
        {
            var profile = Profiles.FirstOrDefault(h => h.Supports(platform));
            if (profile != null)
                return profile;

            throw new PipelineException("There is no supported audio profile for the '" + platform.Name + "' platform!");
        }

        /// <summary>
        /// Returns true if this profile supports audio processing for this platform.
        /// </summary>
        public abstract bool Supports(TargetPlatform platform);

        public abstract void ConvertAudio(TargetPlatform platform, ConversionQuality quality, AudioContent content);

        public abstract void ConvertStreamingAudio(TargetPlatform platform, ConversionQuality quality, AudioContent content, ref string outputFileName);


        protected static int QualityToSampleRate(ConversionQuality quality, AudioFormat format)
        {
            switch (quality)
            {
                case ConversionQuality.Low:
                    return Math.Max(8000, format.SampleRate / 2);
            }

            return Math.Max(8000, format.SampleRate);
        }

        protected static int QualityToBitRate(ConversionQuality quality)
        {
            switch (quality)
            {
                case ConversionQuality.Low:
                    return 96000;
                case ConversionQuality.Medium:
                    return 128000;
            }

            return 192000;
        }
    }
}
