using System;
using System.Collections;
using System.Collections.Generic;

namespace Upbit_Manager.Core.Collections
{
    /// <summary>
    /// 고정된 크기의 순환 버퍼 자료구조입니다.
    /// 실시간 틱 데이터나 최근 가격 이력을 효율적으로 관리하기 위해 사용됩니다.
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
        /// 새로운 아이템을 버퍼에 추가합니다. 
        /// 버퍼가 가득 차면 가장 오래된 데이터를 덮어씁니다.
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

        /// <summary>
        /// 버퍼의 가장 마지막(가장 최근) 요소를 새 값으로 교체합니다.
        /// </summary>
        public void UpdateLast(T newItem)
        {
            if (_count == 0) return;
            int lastIndex = (_end - 1 + _capacity) % _capacity;
            _buffer[lastIndex] = newItem;
        }

        /// <summary>
        /// UpdateLast와 동일한 기능을 수행합니다. (UpbitVolumeSeries 호환용)
        /// </summary>
        public void ReplaceLast(T newItem) => UpdateLast(newItem);

        public void Clear()
        {
            _start = 0;
            _end = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        /// <summary>
        /// 가장 최근에 추가된 데이터를 반환합니다.
        /// </summary>
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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 인덱스를 통해 데이터에 접근합니다 (0이 가장 오래된 데이터).
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