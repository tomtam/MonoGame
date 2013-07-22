using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Graphics
{
    public class EffectParameterCollection : IEnumerable<EffectParameter>
    {
        internal static readonly EffectParameterCollection Empty = new EffectParameterCollection(new EffectParameter[0]);

        private readonly EffectParameter[] _parameters;

        internal EffectParameterCollection(EffectParameter[] parameters)
        {
            _parameters = parameters;
        }

        internal EffectParameterCollection Clone()
        {
            if (_parameters.Length == 0)
                return Empty;

            var parameters = new EffectParameter[_parameters.Length];
            for (var i = 0; i < _parameters.Length; i++)
                parameters[i] = new EffectParameter(_parameters[i]);

            return new EffectParameterCollection(parameters);
        }

        public int Count
        {
            get { return _parameters.Length; }
        }
		
		public EffectParameter this[int index]
		{
			get { return _parameters[index]; }
		}
		
		public EffectParameter this[string name]
        {
            get 
            {
                // TODO: Add a name to parameter lookup table.
				foreach (var parameter in _parameters) 
                {
					if (parameter.Name == name) 
						return parameter;
				}

				return null;
			}
        }

        public ParamEnumerator GetEnumerator()
        {
            return new ParamEnumerator(this);
        }

        IEnumerator<EffectParameter> IEnumerable<EffectParameter>.GetEnumerator()
        {
            return GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct ParamEnumerator : IEnumerator<EffectParameter>
        {
            private readonly EffectParameter[] _items;
            private int _index;

            public ParamEnumerator(EffectParameterCollection collection)
            {
                _items = collection._parameters;
                _index = -1;
            }

            public EffectParameter Current
            {
                get { return _items[_index]; }
            }

            public void Dispose()
            {
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                _index++;
                return _index < _items.Length;
            }

            public void Reset()
            {
                _index = -1;
            }
        }
    }
}
