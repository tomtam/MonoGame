using SharpDX.MediaFoundation;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Media
{
    internal class VideoSampleGrabber : SharpDX.CallbackBase, SampleGrabberSinkCallback
    {
        private readonly object _lock = new object();

        private bool _disposed;
        private byte[] _lastSample;
        private int _lastSampleSize;
        private int _sampleCount;

        public int SampleCount
        {
            get
            {
                lock (_lock)
                {
                    return _sampleCount;
                }
            }
        }

        public void Get(byte[] outData)
        {
            lock (_lock)
            {
                if (_lastSample == null)
                    throw new Exception("No sample data has been received");
                
                if (outData.Length != _lastSampleSize)
                    throw new Exception(string.Format("buffer length '{0}' does not match sample size '{1}'", outData.Length, _lastSampleSize));
                
                Array.Copy(_lastSample, 0, outData, 0, _lastSampleSize);
            }
        }
        
        public void OnProcessSample(Guid guidMajorMediaType, int dwSampleFlags, long llSampleTime, long llSampleDuration, IntPtr sampleBufferRef, int dwSampleSize)
        {
            lock (_lock)
            {
                if (_lastSample == null || _lastSample.Length != dwSampleSize)
                    _lastSample = new byte[dwSampleSize];

                Marshal.Copy(sampleBufferRef, _lastSample, 0, dwSampleSize);
                
                _lastSampleSize = dwSampleSize;
                _sampleCount++;
            }
        }

        public void OnSetPresentationClock(PresentationClock presentationClockRef)
        {

        }

        public void OnShutdown()
        {

        }

        public void OnClockPause(long systemTime)
        {

        }

        public void OnClockRestart(long systemTime)
        {

        }

        public void OnClockSetRate(long systemTime, float flRate)
        {

        }

        public void OnClockStart(long systemTime, long llClockStartOffset)
        {

        }

        public void OnClockStop(long hnsSystemTime)
        {

        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                _lastSample = null;
            }

            _disposed = true;

            // HACK: Looks like disposing the sample grabber callback 
            // object will cause crashes in disposing the topology... 
            // i suspect the dispose here is not releasing the COM 
            // object correctly.
            //
            // For now just don't dispose and allow the potential
            // leak of the callback object.

            //base.Dispose(disposing);
        }
    }
}
