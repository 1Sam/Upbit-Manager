using System;
using System.Collections;
using System.Collections.Generic;

namespace Upbit_Manager.Core.Collections
{
    /// <summary>
    /// 고정된 크기의 순환 버퍼 자료구조입니다.
    /// CopyTo 메서드를 통해 Heap 할당 없이 내부 데이터를 배열로 복사할 수 있습니다.
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

        public void UpdateLast(T newItem)
        {
            if (_count == 0) return;
            int lastIndex = (_end - 1 + _capacity) % _capacity;
            _buffer[lastIndex] = newItem;
        }

        public void Clear()
        {
            _start = 0;
            _end = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        /// <summary>
        /// ⭐ 핵심 개선: 내부 순환 버퍼의 내용을 대상 배열로 고속 복사합니다. (Heap 할당 없음)
        /// </summary>
        public int CopyTo(T[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            int count = Math.Min(_count, destination.Length);
            if (count == 0) return 0;

            if (_start + count <= _capacity)
            {
                // 데이터가 끊기지 않고 연속된 경우
                Array.Copy(_buffer, _start, destination, 0, count);
            }
            else
            {
                // 데이터가 끝부분에서 다시 처음으로 돌아가는 경우 (Wrap-around)
                int firstPart = _capacity - _start;
                Array.Copy(_buffer, _start, destination, 0, firstPart);
                Array.Copy(_buffer, 0, destination, firstPart, count - firstPart);
            }
            return count;
        }

        public T Last
        {
            get
            {
                if (_count == 0) throw new InvalidOperationException("Buffer is empty.");
                int lastIndex = (_end - 1 + _capacity) % _capacity;
                return _buffer[lastIndex];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return _buffer[(_start + i) % _capacity];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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