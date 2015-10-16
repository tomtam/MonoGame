using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using SharpDX;
using SharpDX.MediaFoundation;
using System;
using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Media
{
    public sealed partial class Video : IDisposable
    {        
        internal Topology Topology { get; private set; }
        internal VideoSampleGrabber SampleGrabber { get; private set; }

        private readonly List<SharpDX.MediaFoundation.MediaSource> _sources = new List<SharpDX.MediaFoundation.MediaSource>();
        
        private static Video PlatformFromUri(Uri uri)
        {
            var filename = uri.LocalPath;            
            var video = new Video(filename);
            return video;
        }

        private Guid OutputTypesAttributeGuid = new Guid(0x8eae8cf3, 0xa44f, 0x4306, 0xba, 0x5c, 0xbf, 0x5d, 0xda, 0x24, 0x28, 0x18);
        private Guid RGB32MediaFormatAttributeGuid = new Guid("00000016-0000-0010-8000-00AA00389B71");

        private void PlatformInitialize()
        {
            MediaManagerState.CheckStartup();

            Topology topology;
            MediaFactory.CreateTopology(out topology);
            Topology = topology;

            SharpDX.MediaFoundation.MediaSource mediaSource;
            {
                var resolver = new SourceResolver();
                
                ObjectType otype;
                var source = resolver.CreateObjectFromURL(FileName, SourceResolverFlags.MediaSource, null, out otype);
                mediaSource = source.QueryInterface<SharpDX.MediaFoundation.MediaSource>();

                resolver.Dispose();
                source.Dispose();
            }

            PresentationDescriptor presDesc;
            mediaSource.CreatePresentationDescriptor(out presDesc);

            Duration = TimeSpan.FromMilliseconds(presDesc.Get(PresentationDescriptionAttributeKeys.Duration) * 0.0001f);
            
            for (var i = 0; i < presDesc.StreamDescriptorCount; i++)
            {
                Bool selected;
                StreamDescriptor desc;
                presDesc.GetStreamDescriptorByIndex(i, out selected, out desc);

                if (selected)
                {
                    TopologyNode sourceNode;
                    MediaFactory.CreateTopologyNode(TopologyType.SourceStreamNode, out sourceNode);

                    sourceNode.Set(TopologyNodeAttributeKeys.Source, mediaSource);

                    if (!_sources.Contains(mediaSource))
                        _sources.Add(mediaSource);

                    sourceNode.Set(TopologyNodeAttributeKeys.PresentationDescriptor, presDesc);
                    sourceNode.Set(TopologyNodeAttributeKeys.StreamDescriptor, desc);                                        
                    
                    TopologyNode outputNode;
                    MediaFactory.CreateTopologyNode(TopologyType.OutputNode, out outputNode);
                    
                    var mediaType = desc.MediaTypeHandler.CurrentMediaType;
                    if (mediaType.MajorType == MediaTypeGuids.Video)
                    {
                        SampleGrabber = new VideoSampleGrabber();
                        
                        // Specify that we want the data to come in as RGB32.
                        //var temp = mediaType;
                        var temp = new MediaType();                        
                        temp.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                        temp.Set(MediaTypeAttributeKeys.Subtype, RGB32MediaFormatAttributeGuid);

                        Activate activate;
                        MediaFactory.CreateSampleGrabberSinkActivate(temp, SampleGrabber, out activate);
                        outputNode.Object = activate;                        

                        long dword;
                        int upper, lower;
                        
                        dword = mediaType.Get(MediaTypeAttributeKeys.FrameSize);
                        SharpDXHelper.GetWords(dword, out upper, out lower);
                        Width = upper;
                        Height = lower;

                        dword = mediaType.Get(MediaTypeAttributeKeys.FrameRate);
                        SharpDXHelper.GetWords(dword, out upper, out lower);
                        FramesPerSecond = (float)upper / (float)lower;

                        Topology.AddNode(sourceNode);
                        Topology.AddNode(outputNode);
                        sourceNode.ConnectOutput(0, outputNode, 0);

                        temp.Dispose();
                        activate.Dispose();
                    }
                    else if (mediaType.MajorType == MediaTypeGuids.Audio)
                    {
                        Activate activate;
                        MediaFactory.CreateAudioRendererActivate(out activate);
                        outputNode.Object = activate;

                        // VideoSoundtrackType??

                        Topology.AddNode(sourceNode);
                        Topology.AddNode(outputNode);
                        sourceNode.ConnectOutput(0, outputNode, 0);

                        activate.Dispose();
                    }

                    sourceNode.Dispose();
                    outputNode.Dispose();
                }

                desc.Dispose();
            }

            presDesc.Dispose();
        }

        private void PlatformDispose(bool disposing)
        {
            if (disposing == false)
                return;

            // Cleanup the sources.
            foreach (var source in _sources)
                source.Stop();
            foreach (var source in _sources)
                source.Shutdown();            
            foreach (var source in _sources)
                source.Dispose();
            _sources.Clear();

            if (Topology != null)
            {
                Topology.Dispose();
                Topology = null;
            }

            if (SampleGrabber != null)
            {
                SampleGrabber.Dispose();
                SampleGrabber = null;
            }
        }
    }
}
