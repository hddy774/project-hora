// gzip 폭탄·과대 입력을 역직렬화 도중 중단한다. 원본 스트림의 소유권은 호출자에게 유지한다.
using System;
using System.IO;

namespace Xianxia.Simulation.Persistence
{
    /// <summary>읽은 바이트가 상한을 넘으면 저장 형식 오류로 중단하는 스트림.</summary>
    internal sealed class LimitedReadStream : Stream
    {
        private readonly Stream _source;
        private readonly long _limit;
        private long _read;

        /// <summary>읽기 상한을 지정한다.</summary>
        /// <param name="source">원본 스트림.</param>
        /// <param name="limit">최대 바이트.</param>
        internal LimitedReadStream(Stream source, long limit)
        {
            _source = source;
            _limit = limit;
        }

        /// <inheritdoc/>
        public override bool CanRead => _source.CanRead;
        /// <inheritdoc/>
        public override bool CanSeek => false;
        /// <inheritdoc/>
        public override bool CanWrite => false;
        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();
        /// <inheritdoc/>
        public override long Position { get => _read; set => throw new NotSupportedException(); }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        /// <inheritdoc/>
        public override int Read(Span<byte> buffer)
        {
            int count = _source.Read(buffer);
            _read += count;
            if (_read > _limit)
            {
                throw new SaveFormatException("저장 데이터가 허용 크기를 초과했습니다.");
            }

            return count;
        }

        /// <inheritdoc/>
        public override void Flush() => throw new NotSupportedException();
        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
