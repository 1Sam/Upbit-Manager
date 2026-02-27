using System;
using System.Collections;
using System.Collections.Generic;

namespace Upbit_Manager.UI.Series
{
    // Simple fixed-size ring buffer. When full, oldest items are overwritten.
    public class RingBuffer<T> : IEnumerable<T>
    {
        private readonly T[] _items;
        private int _start = 0;
        private int _count = 0;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
        }

        public int Capacity => _items.Length;
        public int Count => _count;

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _start = 0; _count = 0;
        }

        public void Add(T item)
        {
            if (_count < _items.Length)
            {
                _items[(_start + _count) % _items.Length] = item;
                _count++;
            }
            else
            {
                // overwrite oldest
                _items[_start] = item;
                _start = (_start + 1) % _items.Length;
            }
        }

        public void ReplaceLast(T item)
        {
            if (_count == 0) throw new InvalidOperationException("Buffer is empty");
            int idx = (_start + _count - 1) % _items.Length;
            _items[idx] = item;
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
                return _items[(_start + index) % _items.Length];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++) yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T[] ToArray()
        {
            var arr = new T[_count];
            for (int i = 0; i < _count; i++) arr[i] = this[i];
            return arr;
        }
    }
}
