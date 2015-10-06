using System.Runtime.InteropServices;
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

        private enum SessionState
        {
            Stopped,
            Playing,
            Paused,
            Error,
            Closed,
        };

        private class Callback : IAsyncCallback
        {
            private VideoPlayer _player;
            private MediaSession _session;

            private readonly object _locker = new object();

            private SessionState _state;

            public SessionState State
            {
                get
                {
                    lock (_locker)
                    {
                        // Explode if the video session has gotten into
                        // an error state...  we can't really recover.
                        if (_state == SessionState.Error)
                            throw new Exception("VideoPlayer media playback error!");

                        return _state;
                    }
                }
            }

            public Callback(VideoPlayer player)
            {
                _player = player;
                _session = player._session;
            }

            public void Dispose()
            {
                lock (_locker)
                {
                    _player = null;
                    _session = null;
                }

                if (Shadow != null)
                {
                    //Shadow.Dispose();
                    Shadow = null;
                }
            }

            public IDisposable Shadow { get; set; }

            public void Invoke(AsyncResult asyncResultRef)
            {
                lock (_locker)
                {
                    if (_session == null)
                        return;

                    var ev = _session.EndGetEvent(asyncResultRef);

                    if (ev.TypeInfo == MediaEventTypes.SessionClosed)
                        _state = SessionState.Closed;
                    else
                    {
                        // Handle errors.
                        if (ev.TypeInfo == MediaEventTypes.Error)
                        {
                            var stat = ev.Status;
                            _state = SessionState.Error;
                        }

                            // Handle normal playback states.
                        else if (ev.TypeInfo == MediaEventTypes.SessionEnded ||
                                 ev.TypeInfo == MediaEventTypes.SessionStopped)
                            _state = SessionState.Stopped;
                        else if (ev.TypeInfo == MediaEventTypes.SessionStarted)
                            _state = SessionState.Playing;
                        else if (ev.TypeInfo == MediaEventTypes.SessionPaused)
                            _state = SessionState.Paused;

                            // Let the player know the topology is ready 
                            // so it can setup volume controllers.
                        else if (ev.TypeInfo == MediaEventTypes.SessionTopologyStatus)
                        {
                            var tstat = ev.Get(EventAttributeKeys.TopologyStatus);
                            if (tstat == TopologyStatus.Ready)
                                _player.OnTopologyReady();
                        }

                        _session.BeginGetEvent(this, null);
                    }

                    ev.Dispose();
                }
            }

            public AsyncCallbackFlags Flags { get; private set; }
            public WorkQueueId WorkQueueId { get; private set; }
        }

        #endregion // Supporting Types

        // HACK: Need SharpDX to fix this.
        private static Guid AudioStreamVolumeGuid;

        private MediaSession _session;
        private AudioStreamVolume _volumeController;
        private PresentationClock _clock;
        private Callback _callback;
        private TextureBuffer _textureBuffer;

        private void PlatformInitialize()
        {
            // The GUID is specified in a GuidAttribute attached to the class
            AudioStreamVolumeGuid = Guid.Parse(((GuidAttribute)typeof(AudioStreamVolume).GetCustomAttributes(typeof(GuidAttribute), false)[0]).Value);

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

        private MediaState GetCallbackMediaState()
        {
            // No callback or clock... the video surely isn't playing.
            if (_callback == null || _clock == null)
                return MediaState.Stopped;

            // This will throw if we encounter a video session error.
            var state = _callback.State;

            // Translate to the expected media state.
            switch (state)
            {
                case SessionState.Stopped:
                    return MediaState.Stopped;
                case SessionState.Playing:
                    return MediaState.Playing;
                case SessionState.Paused:
                    return MediaState.Paused;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void BlockForState(SessionState state)
        {
            while (_callback.State != state)
                Thread.Sleep(25);
        }

        private void BlockForState(MediaState state)
        {
            while (GetCallbackMediaState() != state)
                Thread.Sleep(25);
        }

        private void PlatformGetState(ref MediaState result)
        {
            result = GetCallbackMediaState();
        }

        private void PlatformPause()
        {
            _session.Pause();
            BlockForState(MediaState.Paused);
        }

        private void PlatformPlay()
        {
            PlatformStop();

            // create the callback if it hasn't been created yet
            if (_callback == null)
            {
                _callback = new Callback(this);
                _session.BeginGetEvent(_callback, null);
            }
                        
            // Set the new song.
            _session.SetTopology(0, _currentVideo.Topology);

            // Get the new clock.
            _clock = _session.Clock.QueryInterface<PresentationClock>();

            // Start playing.
            _session.Start(null, new Variant());
            BlockForState(MediaState.Playing);
        }

        private void PlatformResume()
        {
            _session.Start(null, new Variant());
            BlockForState(MediaState.Playing);
        }

        private void PlatformStop()
        {
            if (_clock == null)
                return;

            _session.Stop();
            BlockForState(MediaState.Stopped);

            _clock.Dispose();
            _clock = null;
            _volumeController.Dispose();
            _volumeController = null;
        }

        private void SetChannelVolumes()
        {
            if (_volumeController != null && !_volumeController.IsDisposed)
            {
                float volume = _volume;
                if (IsMuted)
                    volume = 0.0f;

                for (int i = 0; i < _volumeController.ChannelCount; i++)
                {
                    _volumeController.SetChannelVolume(i, volume);
                }
            }
        }

        private void PlatformSetVolume()
        {
            if (_volumeController == null)
                return;

            SetChannelVolumes();
        }

        private void PlatformSetIsLooped()
        {
            throw new NotImplementedException();
        }

        private void PlatformSetIsMuted()
        {
            if (_volumeController == null)
                return;

            SetChannelVolumes();
        }

        private TimeSpan PlatformGetPlayPosition()
        {
            if (_clock == null || _clock.TimeSource == null)
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

            // If we don't have a callback we haven't
            // played even once.
            if (_callback != null)
            {
                // Wait for the playback to stop and
                // playback objects to be cleaned up.
                PlatformStop();

                // Wait for the session to close.
                _session.Close();
                BlockForState(SessionState.Closed);

                // Free the callback;
                _callback.Dispose();
                _callback = null;
            }

            // Now shutdown the session... this is syncronous
            // and has no events to block for.
            _session.Shutdown();
            _session.Dispose();
            _session = null;
        }

        private void OnTopologyReady()
        {
            if (_session.IsDisposed)
                return;

            // Get the volume interface.
            IntPtr volumeObjectPtr;
            MediaFactory.GetService(_session, MediaServiceKeys.StreamVolume, AudioStreamVolumeGuid, out volumeObjectPtr);
            _volumeController = CppObject.FromPointer<AudioStreamVolume>(volumeObjectPtr);

            SetChannelVolumes();
        }
    }
}
