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
        internal static extern int sqlite3_column_int(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern long sqlite3_column_int64(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr sqlite3_column_text(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern IntPtr sqlite3_column_blob(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        internal static extern int sqlite3_column_bytes(IntPtr statement, int index);
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
