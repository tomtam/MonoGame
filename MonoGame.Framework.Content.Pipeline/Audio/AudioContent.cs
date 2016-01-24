// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.ObjectModel;
using System.IO;

namespace Microsoft.Xna.Framework.Content.Pipeline.Audio
{
    /// <summary>
    /// Encapsulates and provides operations, such as format conversions, on the 
    /// source audio. This type is produced by the audio importers and used by audio
    /// processors to produce compiled audio assets.
    /// </summary>
    public class AudioContent : ContentItem
    {
        private readonly string _fileName;
        private readonly AudioFileType _fileType;
        private byte[] _data;
        private TimeSpan _duration;
        private AudioFormat _format;
        private int _loopStart;
        private int _loopLength;

        /// <summary>
        /// The current raw audio data.
        /// </summary>
        /// <remarks>This changes from the source data to the output data after conversion.</remarks>
        public ReadOnlyCollection<byte> Data { get { return Array.AsReadOnly(_data); } }

        /// <summary>
        /// The duration of the audio data.
        /// </summary>
        public TimeSpan Duration { get { return _duration; } }

        /// <summary>
        /// The name of the original source audio file.
        /// </summary>
        [ContentSerializerAttribute]
        public string FileName { get { return _fileName; } }

        /// <summary>
        /// The type of the original source audio file.
        /// </summary>
        public AudioFileType FileType { get { return _fileType; } }

        /// <summary>
        /// The current format of the audio data.
        /// </summary>
        /// <remarks>This changes from the source format to the output format after conversion.</remarks>
        public AudioFormat Format { get { return _format; } }

        /// <summary>
        /// The current loop length in samples.
        /// </summary>
        /// <remarks>This changes from the source loop length to the output loop length after conversion.</remarks>
        public int LoopLength { get { return _loopLength; } }

        /// <summary>
        /// The current loop start location in samples.
        /// </summary>
        /// <remarks>This changes from the source loop start to the output loop start after conversion.</remarks>
        public int LoopStart { get { return _loopStart; } }

        /// <summary>
        /// Initializes a new instance of AudioContent.
        /// </summary>
        /// <param name="audioFileName">Name of the audio source file to be processed.</param>
        /// <param name="audioFileType">Type of the processed audio: WAV, MP3 or WMA.</param>
        /// <remarks>Constructs the object from the specified source file, in the format specified.</remarks>
        public AudioContent(string audioFileName, AudioFileType audioFileType)
        {
            _fileName = audioFileName;
            _fileType = audioFileType;

            // Must be opened in read mode otherwise it fails to open
            // read-only files (found in some source control systems)
            using (var fs = new FileStream(audioFileName, FileMode.Open, FileAccess.Read))
            {
                _data = new byte[fs.Length];
                fs.Read(_data, 0, _data.Length);  
            }

            // TODO: We should be populating _duration, _format, _loopLength, 
            // and _loopStart from the source audio file here.
        }

        /// <summary>
        /// Transcodes the source audio to the target format and quality.
        /// </summary>
        /// <param name="formatType">Format to convert this audio to.</param>
        /// <param name="quality">Quality of the processed output audio. For streaming formats, it can be one of the following: Low (96 kbps), Medium (128 kbps), Best (192 kbps).  For WAV formats, it can be one of the following: Low (11kHz ADPCM), Medium (22kHz ADPCM), Best (44kHz PCM)</param>
        /// <param name="saveToFile">
        /// The name of the file that the converted audio should be saved into.  This is used for SongContent, where
        /// the audio is stored external to the XNB file.  If this is null, then the converted audio is stored in
        /// the Data property.
        /// </param>
        [Obsolete("You should prefer to use the conversion methods that take a target platform.")]
        public void ConvertFormat(ConversionFormat formatType, ConversionQuality quality, string saveToFile)
        {
            // Call the legacy conversion code.
            DefaultAudioProfile.ConvertToFormat(this, formatType, quality, saveToFile);
        }

        public void ConvertAudio(TargetPlatform platform, ConversionQuality quality)
        {
            var profile = AudioProfile.ForPlatform(platform);
            profile.ConvertAudio(platform, quality, this);
        }

        public void ConvertStreamingAudio(TargetPlatform platform, ConversionQuality quality, ref string outputFileName)
        {
            var profile = AudioProfile.ForPlatform(platform);
            profile.ConvertStreamingAudio(platform, quality, this, ref outputFileName);
        }

        public void SetData(byte[] data, AudioFormat format, TimeSpan duration, int loopStart, int loopLength)
        {
            _data = data;
            _format = format;
            _duration = duration;
            _loopStart = loopStart;
            _loopLength = loopLength;
        }
    }
}
