// #region License
// /*
// Microsoft Public License (Ms-PL)
// MonoGame - Copyright © 2009 The MonoGame Team
// 
// All rights reserved.
// 
// This license governs use of the accompanying software. If you use the software, you accept this license. If you do not
// accept the license, do not use the software.
// 
// 1. Definitions
// The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under 
// U.S. copyright law.
// 
// A "contribution" is the original software, or any additions or changes to the software.
// A "contributor" is any person that distributes its contribution under this license.
// "Licensed patents" are a contributor's patent claims that read directly on its contribution.
// 
// 2. Grant of Rights
// (A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
// (B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.
// 
// 3. Conditions and Limitations
// (A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
// (B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, 
// your patent license from such contributor to the software ends automatically.
// (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution 
// notices that are present in the software.
// (D) If you distribute any portion of the software in source code form, you may do so only under this license by including 
// a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object 
// code form, you may only do so under a license that complies with this license.
// (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees
// or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent
// permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular
// purpose and non-infringement.
// */
// #endregion License
// 

using System;
using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Graphics
{
    internal class SpriteBatcher : IDisposable
	{
        private const int MaxBatchSize = 2048;
        private const int VerticesPerSprite = 4;
        private const int IndicesPerSprite = 6;
        private const int MaxVertexIndex = MaxBatchSize * VerticesPerSprite;

	    private readonly List<SpriteBatchItem> _batchItemList;

	    private readonly Queue<SpriteBatchItem> _freeBatchItemQueue;

	    private readonly GraphicsDevice _device;

	    private int _startIndex;

#if DIRECTX

	    private readonly IndexBuffer _indexBuffer;
        private readonly DynamicVertexBuffer _vertexBuffer;

#else

        private readonly short[] _index;

#endif

        private readonly VertexPositionColorTexture[] _vertexArray;

		public SpriteBatcher (GraphicsDevice device)
		{
            _device = device;

            _batchItemList = new List<SpriteBatchItem>(MaxBatchSize);
			_freeBatchItemQueue = new Queue<SpriteBatchItem>(MaxBatchSize);

            _vertexArray = new VertexPositionColorTexture[MaxBatchSize * VerticesPerSprite];

            var index = new short[IndicesPerSprite * MaxBatchSize];
            for (var i = 0; i < MaxBatchSize; i++)
            {
                index[i * IndicesPerSprite + 0] = (short)(i * VerticesPerSprite);
                index[i * IndicesPerSprite + 1] = (short)(i * VerticesPerSprite + 1);
                index[i * IndicesPerSprite + 2] = (short)(i * VerticesPerSprite + 2);
                index[i * IndicesPerSprite + 3] = (short)(i * VerticesPerSprite + 1);
                index[i * IndicesPerSprite + 4] = (short)(i * VerticesPerSprite + 3);
                index[i * IndicesPerSprite + 5] = (short)(i * VerticesPerSprite + 2);
            }

#if DIRECTX
            _indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, index.Length, BufferUsage.WriteOnly);
            _indexBuffer.SetData(index);

            _vertexBuffer = new DynamicVertexBuffer(device, VertexPositionColorTexture.VertexDeclaration, _vertexArray.Length, BufferUsage.WriteOnly);
#else
            _index = index;
#endif
		}
		
		public SpriteBatchItem CreateBatchItem()
		{
			SpriteBatchItem item;
			if ( _freeBatchItemQueue.Count > 0 )
				item = _freeBatchItemQueue.Dequeue();
			else
				item = new SpriteBatchItem();
			_batchItemList.Add(item);
			return item;
		}

	    static int CompareTexture ( SpriteBatchItem a, SpriteBatchItem b )
		{
            return ReferenceEquals( a.Texture, b.Texture ) ? 0 : 1;
		}

	    static int CompareDepth ( SpriteBatchItem a, SpriteBatchItem b )
		{
			return a.Depth.CompareTo(b.Depth);
		}

	    static int CompareReverseDepth ( SpriteBatchItem a, SpriteBatchItem b )
		{
			return b.Depth.CompareTo(a.Depth);
		}
		
		public void DrawBatch(SpriteSortMode sortMode)
		{
			// nothing to do
			if ( _batchItemList.Count == 0 )
				return;
			
			// sort the batch items
			switch ( sortMode )
			{
			case SpriteSortMode.Texture :
				_batchItemList.Sort( CompareTexture );
				break;
			case SpriteSortMode.FrontToBack :
				_batchItemList.Sort ( CompareDepth );
				break;
			case SpriteSortMode.BackToFront :
				_batchItemList.Sort ( CompareReverseDepth );
				break;
			}

            var vertexCount = 0;
		    Texture2D currentTexture = null;

#if DIRECTX
            _device.Indices = _indexBuffer;
            _device.SetVertexBuffer(_vertexBuffer);
#endif

			foreach (var item in _batchItemList)
			{
				// If the texture changed, we need to flush 
				// and bind the new texture.
                if (item.Texture != currentTexture)
				{
                    FlushVertexArray(_startIndex, vertexCount);
                    currentTexture = item.Texture;
                    _device.Textures[0] = currentTexture;
                    _startIndex += vertexCount;
                    vertexCount = 0;
				}

                // If the we don't have enough space for another 
                // sprite we need to flush the batch too.
                if (_startIndex + vertexCount >= MaxVertexIndex)
                {
                    FlushVertexArray(_startIndex, vertexCount);
                    _startIndex = vertexCount = 0;
                }

				// Copy the vertex data to the vertex array.
                _vertexArray[vertexCount++] = item.vertexTL;
                _vertexArray[vertexCount++] = item.vertexTR;
                _vertexArray[vertexCount++] = item.vertexBL;
                _vertexArray[vertexCount++] = item.vertexBR;

                // Release the texture and return the item to the queue.
                item.Texture = null;
				_freeBatchItemQueue.Enqueue( item );
			}

			// Flush the remaining sprites.
            FlushVertexArray(_startIndex, vertexCount);
            _startIndex += vertexCount;

			_batchItemList.Clear();
		}

        void FlushVertexArray(int start, int vertexCount)
		{
            if (vertexCount <= 0)
                return;

#if DIRECTX

            _vertexBuffer.SetData(start * VertexPositionColorTexture.VertexDeclaration.VertexStride, 
                _vertexArray,
                0, 
                vertexCount, 
                start == 0 ? SetDataOptions.Discard : SetDataOptions.NoOverwrite);

            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 
                start, 
                0, 
                vertexCount, 
                0, 
                (vertexCount / VerticesPerSprite) * 2);
#else
            
            // UserPrimitive rendering on GL is optimized to render
            // directly from system memory, so we use that here instead
            // of the DynamicVertexBuffer implementation.

            _device.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList, 
                _vertexArray, 
                0,
                vertexCount, 
                _index, 
                0, 
                (vertexCount / 4) * 2, 
                VertexPositionColorTexture.VertexDeclaration);
#endif
        }

        public void Dispose()
        {
#if DIRECTX
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
#endif
        }
	}
}

