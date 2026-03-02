using System;
using System.Collections;
using System.Collections.Generic;

namespace Upbit_Manager.Core.Collections
{
    /// <summary>
    /// Fixed-size circular buffer data structure.
    /// Used to efficiently manage real-time tick data or recent price history.
    /// </summary>
    public class RingBuffer<T> : IEnumerable<T>
    {
        private readonly T[] _buffer;
        private int _start;
        private int _end;
        private int _count;
        private readonly int _capacity;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentException("Capacity must be greater than zero.");
            _capacity = capacity;
            _buffer = new T[capacity];
            _start = 0;
            _end = 0;
            _count = 0;
        }

        public int Count => _count;
        public int Capacity => _capacity;

        /// <summary>
        /// Adds a new item to the buffer.
        /// If the buffer is full, the oldest data is overwritten.
        /// </summary>
        public void Add(T item)
        {
            _buffer[_end] = item;
            _end = (_end + 1) % _capacity;

            if (_count < _capacity)
            {
                _count++;
            }
            else
            {
                _start = (_start + 1) % _capacity;
            }
        }

        public void Clear()
        {
            _start = 0;
            _end = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return _buffer[(_start + i) % _capacity];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Accesses the data via index (0 is the oldest data).
        /// </summary>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
                return _buffer[(_start + index) % _capacity];
            }
        }
    }
}