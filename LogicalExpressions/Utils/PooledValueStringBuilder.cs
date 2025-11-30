using System;
using System.Buffers;

namespace LogicalExpressions.Utils
{
    /// <summary>
    /// Высокоэффективный билдер строк на основе массива символов из пула.
    /// Используется для минимизации аллокаций при конкатенации больших строк.
    /// </summary>
    public ref struct PooledValueStringBuilder
    {
        private char[] _buffer;
        private int _pos;

        /// <summary>
        /// Создаёт новый билдер с начальной ёмкостью <paramref name="initialCapacity"/>.
        /// Буфер берётся из <see cref="ArrayPool{T}.Shared"/>.
        /// </summary>
        /// <param name="initialCapacity">Начальная ёмкость буфера.</param>
        public PooledValueStringBuilder(int initialCapacity = 256)
        {
            _buffer = ArrayPool<char>.Shared.Rent(Math.Max(16, initialCapacity));
            _pos = 0;
        }

        /// <summary>
        /// Текущая длина собранной строки.
        /// </summary>
        public int Length => _pos;

        /// <summary>
        /// Добавляет символ в конец.
        /// </summary>
        /// <param name="c">Символ.</param>
        public void Append(char c)
        {
            EnsureCapacity(_pos + 1);
            _buffer[_pos++] = c;
        }

        /// <summary>
        /// Добавляет строку в конец (ничего не делает для null/пустой строки).
        /// </summary>
        /// <param name="s">Строка.</param>
        public void Append(string? s)
        {
            if (string.IsNullOrEmpty(s)) return;
            var span = s.AsSpan();
            EnsureCapacity(_pos + span.Length);
            span.CopyTo(_buffer.AsSpan(_pos));
            _pos += span.Length;
        }

        /// <summary>
        /// Добавляет последовательность символов.
        /// </summary>
        /// <param name="span">Последовательность символов.</param>
        public void Append(ReadOnlySpan<char> span)
        {
            if (span.Length == 0) return;
            EnsureCapacity(_pos + span.Length);
            span.CopyTo(_buffer.AsSpan(_pos));
            _pos += span.Length;
        }

        /// <summary>
        /// Преобразует содержимое в строку.
        /// </summary>
        public override string ToString() => new string(_buffer, 0, _pos);

        /// <summary>
        /// Преобразует содержимое в строку и возвращает буфер в пул.
        /// После вызова билдер нельзя использовать.
        /// </summary>
        public string ToStringAndDispose()
        {
            var s = new string(_buffer, 0, _pos);
            Dispose();
            return s;
        }

        /// <summary>
        /// Возвращает буфер в пул. После вызова билдер нельзя использовать.
        /// </summary>
        public void Dispose()
        {
            var buf = _buffer;
            _buffer = Array.Empty<char>();
            _pos = 0;
            if (buf.Length > 0)
                ArrayPool<char>.Shared.Return(buf);
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _buffer.Length) return;
            Grow(required);
        }

        private void Grow(int required)
        {
            int newSize = Math.Max(_buffer.Length * 2, required);
            var newBuf = ArrayPool<char>.Shared.Rent(newSize);
            _buffer.AsSpan(0, _pos).CopyTo(newBuf);
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = newBuf;
        }
    }
}
