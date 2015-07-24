using System.Diagnostics;
using System.Threading;
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
            private VideoPlayer _player;

            private readonly object _locker = new object();
            
            public AsyncCallbackFlags Flags { get; private set; }
            public WorkQueueId WorkQueueId { get; private set; }

            public Callback(VideoPlayer player)
            {
                _player = player;
            }

            public void Dispose()
            {
                lock (_locker)
                    _player = null;
            }

            public IDisposable Shadow { get; set; }

            public void Invoke(AsyncResult asyncResultRef)
            {
                lock (_locker)
                {
                    if (_player == null)
                        return;

                    var ev = _player._session.EndGetEvent(asyncResultRef);

                    if (ev.TypeInfo == MediaEventTypes.SessionEnded)
                        _player._state = MediaState.Stopped;

                    ev.Dispose();

                    _player._session.BeginGetEvent(this, null);
                }
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

        private void PlatformGetState(ref MediaState result)
        {
            if (_clock != null)
            {
                ClockState state;
                _clock.GetState(0, out state);

                switch (state)
                {
                    case ClockState.Running:
                        result = MediaState.Playing;
                        return;

                    case ClockState.Paused:
                        result = MediaState.Paused;
                        return;
                }
            }

            result = MediaState.Stopped;
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
            }

            if (_clock != null)
            {
                ClockState state;
                _clock.GetState(0, out state);
                if (state != ClockState.Stopped)
                    _clock.Stop();

                _clock.Dispose();
                _clock = null;
            }

            // Set the new song.
            _session.SetTopology(0, _currentVideo.Topology);

            _volumeController = CppObject.FromPointer<SimpleAudioVolume>(GetVolumeObj(_session));
            _volumeController.Mute = IsMuted;
            _volumeController.MasterVolume = _volume;

            // Get the clock.
            _clock = _session.Clock.QueryInterface<PresentationClock>();

            // create the callback if it hasn't been created yet
            if (_callback == null)
            {
                _callback = new Callback(this);
                _session.BeginGetEvent(_callback, null);
            }
                        
            var varStart = new Variant()
                {
                    Type = VariantType.Default,
                    ElementType = VariantElementType.Long,
                    Value = (long)0,
                };
            _session.Start(null, varStart);
        }

        internal static IntPtr GetVolumeObj(MediaSession session)
        {
            // Get the volume interface - shared between MediaPlayer and VideoPlayer
            const int retries = 10;
            const int sleepTimeFactor = 50;

            var volumeObj = (IntPtr)0;

            //See https://github.com/mono/MonoGame/issues/2620
            //MediaFactory.GetService throws a SharpDX exception for unknown reasons. it appears retrying will solve the problem but there
            //is no specific number of times, nor pause that works. So we will retry N times with an increasing Sleep between each one
            //before finally throwing the error we saw in the first place.
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    MediaFactory.GetService(session, MRPolicyVolumeService, SimpleAudioVolumeGuid, out volumeObj);
                    break;
                }
                catch (SharpDXException)
                {
                    if (i == retries - 1)
                    {
                        throw;
                    }
                    Debug.WriteLine("MediaFactory.GetService failed({0}) sleeping for {1} ms", i + 1, i*sleepTimeFactor);
                    Thread.Sleep(i*sleepTimeFactor); //Sleep for longer and longer times
                }
            }
            return volumeObj;
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

        private void PlatformSetIsLooped()
        {
            throw new NotImplementedException();
        }

        private void PlatformSetIsMuted()
        {
            if (_volumeController == null)
                return;

            _volumeController.Mute = _isMuted;
        }

        private TimeSpan PlatformGetPlayPosition()
        {
            if (_state == MediaState.Stopped)
                return TimeSpan.Zero;

            return TimeSpan.FromTicks(_clock.Time);
        }

        private void PlatformSetPlayPosition(TimeSpan pos)
        {
            var curState = _state;
            if (curState == MediaState.Stopped)
                return;

            if (curState == MediaState.Playing)
                _session.Pause();

            var time = (long)((pos.TotalSeconds) * 10000000);            

            var varStart = new Variant()
                {
                    Type = VariantType.Default,
                    ElementType = VariantElementType.Long,
                    Value = time,
                };
            _session.Start(Guid.Empty, varStart);

            if (curState == MediaState.Paused)
                _session.Pause();
        }

        private void PlatformDispose(bool disposing)
        {
            if (_textureBuffer != null)
            {
                _textureBuffer.Dispose();
                _textureBuffer = null;
            }

            if (_callback != null)
            {
                _callback.Dispose();
                _callback = null;
            }

            _session.Stop();
            _session.Shutdown();
            _state = MediaState.Stopped;

            if (_volumeController != null)
            {
                _volumeController.Dispose();
                _volumeController = null;
            }

            if (_clock != null)
            {
                ClockState state;
                _clock.GetState(0, out state);
                if (state != ClockState.Stopped)
                    _clock.Stop();
                _clock.Dispose();
                _clock = null;
            }

            _session.Dispose();
            _session = null;
        }
    }
}
