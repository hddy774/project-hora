using System.Runtime.InteropServices;

namespace Chronicles.Persistence;

/// <summary>
/// Small Windows system-SQLite adapter. The PC build uses the SQLite engine shipped
/// by Windows (winsqlite3.dll), so the save format is a real SQLite database without
/// adding a runtime NuGet dependency.
/// </summary>
internal sealed class WindowsSqlite : IDisposable
{
    private const int Ok = 0;
    private const int Row = 100;
    private const int Done = 101;
    private const int OpenReadOnly = 0x00000001;
    private const int OpenReadWrite = 0x00000002;
    private const int OpenCreate = 0x00000004;
    private static readonly IntPtr Transient = new(-1);

    private IntPtr _database;

    private WindowsSqlite(IntPtr database)
    {
        _database = database;
        var result = Native.sqlite3_busy_timeout(_database, 5_000);
        if (result != Ok)
        {
            throw new SqliteStoreException(GetError(_database, result), result);
        }
    }

    public static WindowsSqlite Open(string path, bool readOnly)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("PC 슬롯 저장은 Windows system SQLite를 사용합니다.");
        }

        var flags = readOnly ? OpenReadOnly : OpenReadWrite | OpenCreate;
        var filename = Marshal.StringToCoTaskMemUTF8(Path.GetFullPath(path));
        int result;
        IntPtr database;
        try
        {
            result = Native.sqlite3_open_v2(filename, out database, flags, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeCoTaskMem(filename);
        }
        if (result == Ok)
        {
            return new WindowsSqlite(database);
        }

        var message = database == IntPtr.Zero
            ? $"SQLite 데이터베이스를 열 수 없습니다 ({result})."
            : GetError(database, result);
        if (database != IntPtr.Zero)
        {
            var closeResult = Native.sqlite3_close_v2(database);
            GC.KeepAlive(closeResult);
        }

        throw new SqliteStoreException(message, result);
    }

    public void Execute(string sql)
    {
        EnsureNotDisposed();
        var sqlUtf8 = Marshal.StringToCoTaskMemUTF8(sql);
        int result;
        IntPtr error;
        try
        {
            result = Native.sqlite3_exec(_database, sqlUtf8, IntPtr.Zero, IntPtr.Zero, out error);
        }
        finally
        {
            Marshal.FreeCoTaskMem(sqlUtf8);
        }
        if (result == Ok)
        {
            return;
        }

        var message = error == IntPtr.Zero
            ? GetError(_database, result)
            : Marshal.PtrToStringUTF8(error) ?? GetError(_database, result);
        if (error != IntPtr.Zero)
        {
            Native.sqlite3_free(error);
        }

        throw new SqliteStoreException(message, result);
    }

    public Statement Prepare(string sql)
    {
        EnsureNotDisposed();
        var sqlUtf8 = Marshal.StringToCoTaskMemUTF8(sql);
        int result;
        IntPtr statement;
        try
        {
            result = Native.sqlite3_prepare_v2(_database, sqlUtf8, -1, out statement, IntPtr.Zero);
        }
        finally
        {
            Marshal.FreeCoTaskMem(sqlUtf8);
        }
        if (result != Ok)
        {
            throw new SqliteStoreException(GetError(_database, result), result);
        }

        return new Statement(this, statement);
    }

    public IncrementalBlob OpenBlob(
        string table,
        string column,
        long rowId,
        bool writable)
    {
        EnsureNotDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(column);

        var databaseName = Marshal.StringToCoTaskMemUTF8("main");
        var tableName = Marshal.StringToCoTaskMemUTF8(table);
        var columnName = Marshal.StringToCoTaskMemUTF8(column);
        int result;
        IntPtr blob;
        try
        {
            result = Native.sqlite3_blob_open(
                _database,
                databaseName,
                tableName,
                columnName,
                rowId,
                writable ? 1 : 0,
                out blob);
        }
        finally
        {
            Marshal.FreeCoTaskMem(databaseName);
            Marshal.FreeCoTaskMem(tableName);
            Marshal.FreeCoTaskMem(columnName);
        }

        if (result != Ok)
        {
            throw new SqliteStoreException(GetError(_database, result), result);
        }

        return new IncrementalBlob(this, blob, writable);
    }

    public string QueryScalarText(string sql)
    {
        using var statement = Prepare(sql);
        if (!statement.StepRow())
        {
            throw new InvalidDataException("SQLite scalar 결과가 없습니다.");
        }

        return statement.GetText(0);
    }

    public long QueryScalarInt64(string sql)
    {
        using var statement = Prepare(sql);
        if (!statement.StepRow())
        {
            throw new InvalidDataException("SQLite scalar 결과가 없습니다.");
        }

        return statement.GetInt64(0);
    }

    public void Dispose()
    {
        var database = Interlocked.Exchange(ref _database, IntPtr.Zero);
        if (database != IntPtr.Zero)
        {
            var closeResult = Native.sqlite3_close_v2(database);
            GC.KeepAlive(closeResult);
        }
    }

    private void EnsureNotDisposed() =>
        ObjectDisposedException.ThrowIf(_database == IntPtr.Zero, this);

    private static string GetError(IntPtr database, int result)
    {
        var pointer = Native.sqlite3_errmsg(database);
        var detail = pointer == IntPtr.Zero ? "알 수 없는 SQLite 오류" : Marshal.PtrToStringUTF8(pointer);
        return $"SQLite 오류 {result}: {detail}";
    }

    internal sealed class Statement : IDisposable
    {
        private readonly WindowsSqlite _owner;
        private IntPtr _statement;

        internal Statement(WindowsSqlite owner, IntPtr statement)
        {
            _owner = owner;
            _statement = statement;
        }

        public void BindInt32(int index, int value) =>
            ThrowOnError(Native.sqlite3_bind_int(_statement, index, value));

        public void BindInt64(int index, long value) =>
            ThrowOnError(Native.sqlite3_bind_int64(_statement, index, value));

        public void BindBlob(int index, byte[] value)
        {
            ArgumentNullException.ThrowIfNull(value);
            ThrowOnError(Native.sqlite3_bind_blob(_statement, index, value, value.Length, Transient));
        }

        public void BindZeroBlob(int index, int bytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bytes);
            ThrowOnError(Native.sqlite3_bind_zeroblob(_statement, index, bytes));
        }

        public void Execute()
        {
            var result = Native.sqlite3_step(_statement);
            if (result != Done)
            {
                ThrowOnError(result);
            }
        }

        public bool StepRow()
        {
            var result = Native.sqlite3_step(_statement);
            if (result == Row)
            {
                return true;
            }

            if (result == Done)
            {
                return false;
            }

            ThrowOnError(result);
            return false;
        }

        public int GetInt32(int index) => Native.sqlite3_column_int(_statement, index);
        public long GetInt64(int index) => Native.sqlite3_column_int64(_statement, index);

        public string GetText(int index)
        {
            var pointer = Native.sqlite3_column_text(_statement, index);
            return pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
        }

        public byte[] GetBlob(int index)
        {
            var length = Native.sqlite3_column_bytes(_statement, index);
            if (length < 0)
            {
                throw new InvalidDataException("SQLite blob 길이가 잘못되었습니다.");
            }

            if (length == 0)
            {
                return Array.Empty<byte>();
            }

            var pointer = Native.sqlite3_column_blob(_statement, index);
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidDataException("SQLite blob 데이터가 없습니다.");
            }

            var result = new byte[length];
            Marshal.Copy(pointer, result, 0, length);
            return result;
        }

        public void Dispose()
        {
            var statement = Interlocked.Exchange(ref _statement, IntPtr.Zero);
            if (statement != IntPtr.Zero)
            {
                var finalizeResult = Native.sqlite3_finalize(statement);
                GC.KeepAlive(finalizeResult);
            }
        }

        private void ThrowOnError(int result)
        {
            if (result != Ok)
            {
                throw new SqliteStoreException(GetError(_owner._database, result), result);
            }
        }
    }

    internal sealed class IncrementalBlob : Stream
    {
        private readonly WindowsSqlite _owner;
        private readonly bool _writable;
        private IntPtr _blob;
        private readonly int _length;
        private int _position;

        internal IncrementalBlob(WindowsSqlite owner, IntPtr blob, bool writable)
        {
            _owner = owner;
            _blob = blob;
            _writable = writable;
            _length = Native.sqlite3_blob_bytes(blob);
            if (_length < 0)
            {
                Dispose();
                throw new InvalidDataException("SQLite incremental BLOB 길이가 잘못되었습니다.");
            }
        }

        public override bool CanRead => _blob != IntPtr.Zero && !_writable;
        public override bool CanSeek => _blob != IntPtr.Zero;
        public override bool CanWrite => _blob != IntPtr.Zero && _writable;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set
            {
                ObjectDisposedException.ThrowIf(_blob == IntPtr.Zero, this);
                if (value < 0 || value > _length)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _position = checked((int)value);
            }
        }

        public override void Flush()
        {
            ObjectDisposedException.ThrowIf(_blob == IntPtr.Zero, this);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(_blob == IntPtr.Zero, this);
            if (!CanRead)
            {
                throw new NotSupportedException("쓰기용 SQLite BLOB은 읽을 수 없습니다.");
            }

            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (offset > buffer.Length - count)
            {
                throw new ArgumentException("버퍼 범위가 잘못되었습니다.");
            }

            var remaining = _length - _position;
            var requested = Math.Min(count, remaining);
            if (requested == 0)
            {
                return 0;
            }

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                ThrowOnError(Native.sqlite3_blob_read(
                    _blob,
                    IntPtr.Add(handle.AddrOfPinnedObject(), offset),
                    requested,
                    _position));
            }
            finally
            {
                handle.Free();
            }

            _position += requested;
            return requested;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(_blob == IntPtr.Zero, this);
            if (!CanWrite)
            {
                throw new NotSupportedException("읽기용 SQLite BLOB에는 기록할 수 없습니다.");
            }

            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (offset > buffer.Length - count || count > _length - _position)
            {
                throw new ArgumentException("버퍼 또는 SQLite BLOB 범위가 잘못되었습니다.");
            }

            if (count == 0)
            {
                return;
            }

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                ThrowOnError(Native.sqlite3_blob_write(
                    _blob,
                    IntPtr.Add(handle.AddrOfPinnedObject(), offset),
                    count,
                    _position));
            }
            finally
            {
                handle.Free();
            }

            _position += count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_position + offset),
                SeekOrigin.End => checked(_length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            Position = target;
            return target;
        }

        public override void SetLength(long value) =>
            throw new NotSupportedException("SQLite incremental BLOB 길이는 zeroblob INSERT에서 고정됩니다.");

        protected override void Dispose(bool disposing)
        {
            var blob = Interlocked.Exchange(ref _blob, IntPtr.Zero);
            if (blob != IntPtr.Zero)
            {
                var result = Native.sqlite3_blob_close(blob);
                GC.KeepAlive(result);
            }

            base.Dispose(disposing);
        }

        private void ThrowOnError(int result)
        {
            if (result != Ok)
            {
                throw new SqliteStoreException(GetError(_owner._database, result), result);
            }
        }
    }

    private static class Native
    {
        private const string Library = "winsqlite3";

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_open_v2(
            IntPtr filename,
            out IntPtr database,
            int flags,
            IntPtr vfs);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_close_v2(IntPtr database);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_exec(
            IntPtr database,
            IntPtr sql,
            IntPtr callback,
            IntPtr callbackArgument,
            out IntPtr errorMessage);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern void sqlite3_free(IntPtr value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr sqlite3_errmsg(IntPtr database);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_prepare_v2(
            IntPtr database,
            IntPtr sql,
            int bytes,
            out IntPtr statement,
            IntPtr tail);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_step(IntPtr statement);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_finalize(IntPtr statement);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_bind_int(IntPtr statement, int index, int value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_bind_blob(
            IntPtr statement,
            int index,
            byte[] value,
            int bytes,
            IntPtr destructor);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_bind_zeroblob(IntPtr statement, int index, int bytes);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_column_int(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern long sqlite3_column_int64(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr sqlite3_column_text(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr sqlite3_column_blob(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_column_bytes(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_blob_open(
            IntPtr database,
            IntPtr databaseName,
            IntPtr tableName,
            IntPtr columnName,
            long rowId,
            int flags,
            out IntPtr blob);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_blob_bytes(IntPtr blob);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_blob_read(
            IntPtr blob,
            IntPtr buffer,
            int bytes,
            int offset);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_blob_write(
            IntPtr blob,
            IntPtr buffer,
            int bytes,
            int offset);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_blob_close(IntPtr blob);
    }
}

internal sealed class SqliteStoreException : IOException
{
    public SqliteStoreException(string message, int resultCode)
        : base(message)
    {
        ResultCode = resultCode;
    }

    public int ResultCode { get; }
}
