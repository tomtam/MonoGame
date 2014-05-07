using Microsoft.Xna.Framework.Graphics;
using SharpDX;
using SharpDX.MediaFoundation;
using SharpDX.Win32;
using System;

namespace Microsoft.Xna.Framework.Media
{    
    public sealed partial class VideoPlayer : IDisposable
    {
        #region Supporting Types

        private class TextureBuffer : IDisposable
        {
            private readonly Texture2D[] _frames;
            private int _index;

            public readonly int Width;
            public readonly int Height;

            public TextureBuffer(GraphicsDevice device, int width, int height)
            {
                _frames = new Texture2D[2];
                for (var i = 0; i < _frames.Length; i++)
                {
                    var tex = new Texture2D(device, width, height, false, SurfaceFormat.Bgr32);
                    _frames[i] = tex;
                }

                Width = width;
                Height = height;
            }

            public Texture2D Get()
            {
                return _frames[_index];
            }

            public void Set(byte[] data)
            {
                _index = (_index + 1) % 2;
                _frames[_index].SetData(data);
            }

            public void Init(byte[] data)
            {
                for (var i = 0; i < _frames.Length; i++)
                    _frames[i].SetData(data);
            }

            public void Dispose()
            {
                foreach (var tex in _frames)
                    tex.Dispose();
            }
        }

        private class Callback : IAsyncCallback
        {
            private readonly VideoPlayer _player;

            public AsyncCallbackFlags Flags { get; private set; }
            public WorkQueueId WorkQueueId { get; private set; }

            public Callback(VideoPlayer player)
            {
                _player = player;
            }

            public void Dispose()
            {
            }

            public IDisposable Shadow { get; set; }

            public void Invoke(AsyncResult asyncResultRef)
            {
                var ev = _player._session.EndGetEvent(asyncResultRef);
                if (ev.TypeInfo == MediaEventTypes.SessionEnded)
                {
                    _player._state = MediaState.Stopped;
                }

                _player._session.BeginGetEvent(this, null);
            }
        }

        #endregion

        // HACK: Need SharpDX to fix this.
        private static readonly Guid MRPolicyVolumeService = Guid.Parse("1abaa2ac-9d3b-47c6-ab48-c59506de784d");
        private static readonly Guid SimpleAudioVolumeGuid = Guid.Parse("089EDF13-CF71-4338-8D13-9E569DBDC319");  

        private MediaSession _session;
        private SimpleAudioVolume _volumeController;
        private PresentationClock _clock;
        private Callback _callback;
        private TextureBuffer _textureBuffer;
       
        private void PlatformInitialize()
        {
            MediaManagerState.CheckStartup();
            MediaFactory.CreateMediaSession(null, out _session);
        }

        private Texture2D PlatformGetTexture()
        {
            var sampleGrabber = _currentVideo.SampleGrabber;

            if (_textureBuffer != null && (_textureBuffer.Width != _currentVideo.Width || _textureBuffer.Height != _currentVideo.Height))
            {
                _textureBuffer.Dispose();
                _textureBuffer = null;
            }

            if (_textureBuffer == null)
            {
                _textureBuffer = new TextureBuffer(Game.Instance.GraphicsDevice, _currentVideo.Width, _currentVideo.Height);

                if (sampleGrabber.TextureData != null)
                    _textureBuffer.Init(sampleGrabber.TextureData);
            }

            var texture = _textureBuffer.Get();
            if (sampleGrabber.Dirty)
            {
                if (sampleGrabber.TextureData != null)
                    _textureBuffer.Set(sampleGrabber.TextureData);
                sampleGrabber.Dirty = false;
            }

            return texture;
        }

        private void PlatformPause()
        {
            _session.Pause();
        }

        private void PlatformPlay()
        {
            // Cleanup the last song first.
            if (State != MediaState.Stopped)
            {
                _session.Stop();
                _volumeController.Dispose();
                _clock.Dispose();
            }

            // Set the new song.
            _session.SetTopology(0, _currentVideo.Topology);

            // Get the volume interface.
            IntPtr volumeObj;

            try
            {
                MediaFactory.GetService(_session, MRPolicyVolumeService, SimpleAudioVolumeGuid, out volumeObj);

                _volumeController = CppObject.FromPointer<SimpleAudioVolume>(volumeObj);
                _volumeController.Mute = IsMuted;
                _volumeController.MasterVolume = _volume;
            }
            catch
            {
                // Do we support videos without audio tracks?
            }

            // Get the clock.
            _clock = _session.Clock.QueryInterface<PresentationClock>();

            // create the callback if it hasn't been created yet
            if (_callback == null)
            {
                _callback = new Callback(this);
                _session.BeginGetEvent(_callback, null);
            }

            // Start playing.
            var varStart = new Variant();
            _session.Start(null, varStart);
        }

        private void PlatformResume()
        {
            var varStart = new Variant();
            _session.Start(null, varStart);
        }

        private void PlatformStop()
        {
            _session.Stop();
        }

        private void PlatformSetVolume()
        {
            if (_volumeController == null)
                return;

            _volumeController.MasterVolume = _volume;
        }

        private TimeSpan PlatformGetPlayPosition()
        {
            return TimeSpan.FromMilliseconds(_clock.Time * 0.0001f);
        }

        private void PlatformDispose()
        {
            if (_textureBuffer != null)
            {
                _textureBuffer.Dispose();
                _textureBuffer = null;
            }
        }
    }
}
