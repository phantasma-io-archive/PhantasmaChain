using System;
using System.Collections.Generic;

namespace Phantasma.Core.Utils
{
    public class Cache<T>
    {
        private List<KeyValuePair<DateTime, T>> _content;

        public readonly int MaxCount;
        public readonly TimeSpan MaxDuration;

        public IEnumerable<T> Items
        {
            get
            {
                foreach (var item in _content)
                {
                    yield return item.Value;
                }

                yield break;
            }
        }

        public Cache(int maxItems, TimeSpan maxDuration)
        {
            this.MaxCount = maxItems;
            this.MaxDuration = maxDuration;
            this._content = new List<KeyValuePair<DateTime, T>>(maxItems);
        }

        public void Add(T item)
        {
            // keep removing all items that are too old
            while (_content.Count > 0)
            {
                var date = _content[0].Key;
                var diff = DateTime.UtcNow - date;
                if (diff >= MaxDuration)
                {
                    _content.RemoveAt(0);
                }
                else
                {
                    break;
                }
            }

            if (_content.Count >= MaxCount - 1)
            {
                _content.RemoveAt(0);
            }

            _content.Add(new KeyValuePair<DateTime, T>(DateTime.UtcNow, item));
        }
    }
}
