using System.Security.Cryptography;

namespace Chronicles.Game;

/// <summary>
/// 호출 한 번의 canonical UTF8 출력을 보관하지 않고 SHA-256에 전달하는 비파일 stream이다.
/// serializer의 바이트 순서·예외는 유지하고 완료 또는 실패 후 해시 자원을 해제한다.
/// </summary>
internal sealed class CanonicalHashSink : Stream
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>지금까지 전달된 바이트의 기존 소문자 SHA-256 표기를 반환한다.</summary>
    internal string CompleteHash() => Convert.ToHexStringLower(hash.GetHashAndReset());

    public override void Write(byte[] buffer, int offset, int count)
    {
        hash.AppendData(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        hash.AppendData(buffer);
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hash.Dispose();
        }

        base.Dispose(disposing);
    }
}
