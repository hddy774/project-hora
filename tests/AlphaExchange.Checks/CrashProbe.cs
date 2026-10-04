using System.Diagnostics;
using Microsoft.Data.Sqlite;

static class CrashProbe
{
    public static void AbruptWrite(string path,string run)
    {
        using var connection=new SqliteConnection($"Data Source={path};Pooling=False"); connection.Open();
        using var transaction=connection.BeginTransaction(); using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="INSERT INTO hours(run,hour,resolution,cap,data,hash) SELECT run,(SELECT hour+1 FROM runs WHERE id=$run),1,cap,data,hash FROM hours WHERE run=$run ORDER BY hour DESC LIMIT 1";
        command.Parameters.AddWithValue("$run",run); command.ExecuteNonQuery();
        command.CommandText="UPDATE runs SET hour=hour+1 WHERE id=$run"; command.ExecuteNonQuery();
        // Abrupt process exit bypasses using/finally: SQLite must recover the uncommitted WAL itself.
        Environment.Exit(23);
    }
    public static int Run(string path,string run)
    {
        var start=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),"..","..","..",OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")))
        { RedirectStandardError=true,RedirectStandardOutput=true };
        foreach(string arg in new[] { typeof(CrashProbe).Assembly.Location,"--crash-write",path,run }) start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;
        if(!process.WaitForExit(15000)) { process.Kill(); throw new Exception("Crash probe timed out"); }
        if(process.ExitCode!=23) throw new Exception("Crash probe failed: "+process.StandardError.ReadToEnd());
        return process.ExitCode;
    }
}
