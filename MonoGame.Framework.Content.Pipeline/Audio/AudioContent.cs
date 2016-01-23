// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Microsoft.Xna.Framework.Content.Pipeline.Audio
{
    /// <summary>
    /// Encapsulates and provides operations, such as format conversions, on the source audio. This type is produced by the audio importers and used by audio processors to produce compiled audio assets.
    /// </summary>
    public class AudioContent : ContentItem
    {
        private readonly string _fileName;
        private readonly AudioFileType _fileType;
        private List<byte> _data;
        private TimeSpan _duration;
        private AudioFormat _format;
        private int _loopLength;
        private int _loopStart;

        /// <summary>
        /// The current raw audio data.
        /// </summary>
        /// <remarks>This changes from the source data to the output data after conversion.</remarks>
        public ReadOnlyCollection<byte> Data { get { return _data.AsReadOnly(); } }

        /// <summary>
        /// The duration of the audio data in milliseconds.
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
                var data = new byte[fs.Length];
                fs.Read(data, 0, data.Length);
                _data = data.ToList();
            }

            // TODO: We should be populating _duration, _format, _loopLength, 
            // and _loopStart from the source audio file here.
        }

        int QualityToSampleRate(ConversionQuality quality)
        {
            switch (quality)
            {
                case ConversionQuality.Low:
                    return Math.Max(8000, _format.SampleRate / 2);
            }

            return Math.Max(8000, _format.SampleRate);
        }

        int QualityToBitRate(ConversionQuality quality)
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
        public void ConvertFormat(ConversionFormat formatType, ConversionQuality quality, string saveToFile)
        {
            var temporarySource = Path.GetTempFileName();
            var temporaryOutput = Path.GetTempFileName();
            try
            {
                using (var fs = new FileStream(temporarySource, FileMode.Create, FileAccess.Write))
                {
                    var dataBytes = _data.ToArray();
                    fs.Write(dataBytes, 0, dataBytes.Length);
                }

                string ffmpegCodecName, ffmpegMuxerName;
                int format;
                switch (formatType)
                {
                    case ConversionFormat.Adpcm:
                        // ADPCM Microsoft 
                        ffmpegCodecName = "adpcm_ms";
                        ffmpegMuxerName = "wav";
                        format = 0x0002; /* WAVE_FORMAT_ADPCM */
                        break;
                    case ConversionFormat.Pcm:
                        // PCM signed 16-bit little-endian
                        ffmpegCodecName = "pcm_s16le";
                        ffmpegMuxerName = "s16le";
                        format = 0x0001; /* WAVE_FORMAT_PCM */
                        break;
                    case ConversionFormat.WindowsMedia:
                        // Windows Media Audio 2
                        ffmpegCodecName = "wmav2";
                        ffmpegMuxerName = "asf";
                        format = 0x0161; /* WAVE_FORMAT_WMAUDIO2 */
                        break;
                    case ConversionFormat.Xma:
                        throw new NotSupportedException(
                            "XMA is not a supported encoding format. It is specific to the Xbox 360.");
                    case ConversionFormat.ImaAdpcm:
                        // ADPCM IMA WAV
                        ffmpegCodecName = "adpcm_ima_wav";
                        ffmpegMuxerName = "wav";
                        format = 0x0011; /* WAVE_FORMAT_IMA_ADPCM */
                        break;
                    case ConversionFormat.Aac:
                        // AAC (Advanced Audio Coding)
                        // Requires -strict experimental
                        ffmpegCodecName = "aac";
                        ffmpegMuxerName = "ipod";
                        format = 0x0000; /* WAVE_FORMAT_UNKNOWN */
                        break;
                    case ConversionFormat.Vorbis:
                        // Vorbis
                        ffmpegCodecName = "libvorbis";
                        ffmpegMuxerName = "ogg";
                        format = 0x0000; /* WAVE_FORMAT_UNKNOWN */
                        break;
                    default:
                        // Unknown format
                        throw new NotSupportedException();
                }

                string ffmpegStdout, ffmpegStderr;
                var ffmpegExitCode = ExternalTool.Run(
                    "ffmpeg",
                    string.Format(
                        "-y -i \"{0}\" -vn -c:a {1} -b:a {2} -f:a {3} -strict experimental \"{4}\"",
                        temporarySource,
                        ffmpegCodecName,
                        QualityToBitRate(quality),
                        ffmpegMuxerName,
                        temporaryOutput),
                    out ffmpegStdout,
                    out ffmpegStderr);
                if (ffmpegExitCode != 0)
                {
                    throw new InvalidOperationException("ffmpeg exited with non-zero exit code: \n" + ffmpegStdout + "\n" + ffmpegStderr);
                }

                byte[] rawData;
                using (var fs = new FileStream(temporaryOutput, FileMode.Open, FileAccess.Read))
                {
                    rawData = new byte[fs.Length];
                    fs.Read(rawData, 0, rawData.Length);
                }

                if (saveToFile != null)
                {
                    using (var fs = new FileStream(saveToFile, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(rawData, 0, rawData.Length);
                    }

                    _data = null;
                }
                else
                {
                    _data = rawData.ToList();
                }

                string ffprobeStdout, ffprobeStderr;
                var ffprobeExitCode = ExternalTool.Run(
                    "ffprobe",
                    string.Format("-i \"{0}\" -show_entries streams -v quiet -of flat", temporarySource),
                    out ffprobeStdout,
                    out ffprobeStderr);
                if (ffprobeExitCode != 0)
                {
                    throw new InvalidOperationException("ffprobe exited with non-zero exit code.");
                }

                // Set default values if information is not available.
                int averageBytesPerSecond = 0;
                int bitsPerSample = 0;
                int blockAlign = 0;
                int channelCount = 0;
                int sampleRate = 0;
                double durationInSeconds = 0;

                var numberFormat = System.Globalization.CultureInfo.InvariantCulture.NumberFormat;
                foreach (var line in ffprobeStdout.Split(new[] { '\r', '\n', '\0' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = line.Split(new[] { '=' }, 2);

                    switch (kv[0])
                    {
                        case "streams.stream.0.sample_rate":
                            sampleRate = int.Parse(kv[1].Trim('"'), numberFormat);
                            break;
                        case "streams.stream.0.bits_per_sample":
                            bitsPerSample = int.Parse(kv[1].Trim('"'), numberFormat);
                            break;
                        case "streams.stream.0.duration":
                            durationInSeconds = double.Parse(kv[1].Trim('"'), numberFormat);
                            break;
                        case "streams.stream.0.channels":
                            channelCount = int.Parse(kv[1].Trim('"'), numberFormat);
                            break;
                        case "streams.stream.0.bit_rate":
                            averageBytesPerSecond = (int.Parse(kv[1].Trim('"'), numberFormat) / 8);
                            break;
                    }
                }

                // Calculate blockAlign.
                switch (formatType)
                {
                    case ConversionFormat.Pcm:
                        // Block alignment value is the number of bytes in an atomic unit (that is, a block) of audio for a particular format. For Pulse Code Modulation (PCM) formats, the formula for calculating block alignment is as follows: 
                        //  •   Block Alignment = Bytes per Sample x Number of Channels
                        // For example, the block alignment value for 16-bit PCM format mono audio is 2 (2 bytes per sample x 1 channel). For 16-bit PCM format stereo audio, the block alignment value is 4.
                        // https://msdn.microsoft.com/en-us/library/system.speech.audioformat.speechaudioformatinfo.blockalign(v=vs.110).aspx
                        blockAlign = (bitsPerSample / 8) * channelCount;
                        break;
                    default:
                        // blockAlign is not available from ffprobe (and may or may not
                        // be relevant for non-PCM formats anyway)
                        break;
                }


                _duration = TimeSpan.FromSeconds(durationInSeconds);
                _format = new AudioFormat(
                    averageBytesPerSecond,
                    bitsPerSample,
                    blockAlign,
                    channelCount,
                    format,
                    sampleRate);

                // Loop start and length in number of samples. Defaults to entire sound
                _loopStart = 0;
                if (_data != null && bitsPerSample > 0 && channelCount > 0)
                    _loopLength = _data.Count / ((bitsPerSample / 8) * channelCount);
                else
                    _loopLength = 0;
            }
            finally
            {
                ExternalTool.DeleteFile(temporarySource);
                ExternalTool.DeleteFile(temporaryOutput);
            }
        }
	}
}
