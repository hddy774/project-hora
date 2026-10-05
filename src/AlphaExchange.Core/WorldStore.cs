using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AlphaExchange.Core;
public sealed partial class GameStore
{
    readonly Dictionary<string,List<EventRecord>> pendingActivities=[];
    readonly Dictionary<string,List<EventRecord>> pendingWorldVotes=[];
    void AttachWorld(GameEngine engine,string run)
    {
        lock(pending)
        {
            pendingActivities.TryAdd(run,[]); pendingWorldVotes.TryAdd(run,[]);
            foreach(var activity in engine.World.Activities) pendingActivities[run].Add(new(activity.Id,activity.Minute/60,JsonSerializer.Serialize(activity)));
            foreach(var vote in engine.World.Votes) pendingWorldVotes[run].Add(new(vote.Id,vote.Minute/60,JsonSerializer.Serialize(vote)));
        }
        engine.ActivityRecorded+=a=> { lock(pending) pendingActivities[run].Add(new(a.Id,a.Minute/60,JsonSerializer.Serialize(a))); };
        engine.WorldVoteRecorded+=v=> { lock(pending) pendingWorldVotes[run].Add(new(v.Id,v.Minute/60,JsonSerializer.Serialize(v))); };
    }
    static void WriteWorld(SqliteCommand command,SaveSnapshot snapshot)
    {
        foreach(var item in snapshot.Activities) WriteWorldRecord(command,"activities",snapshot.RunId,item);
        foreach(var item in snapshot.WorldVotes) WriteWorldRecord(command,"world_votes",snapshot.RunId,item);
    }
    static void WriteWorldRecord(SqliteCommand command,string table,string run,EventRecord item)
    {
        if(table=="activities")
        {
            var a=JsonSerializer.Deserialize<PersonActivity>(item.Json) ?? throw new InvalidDataException("활동 기록 오류");
            if(a.Id!=item.Id || a.Minute/60!=item.Hour || a.Minute<0 || a.ActorId is <1 or >200 || a.Amount<0) throw new InvalidDataException("활동 기록 범위 오류");
        }
        else
        {
            var v=JsonSerializer.Deserialize<WorldVote>(item.Json) ?? throw new InvalidDataException("표결 기록 오류");
            if(v.Id!=item.Id || v.Minute/60!=item.Hour || v.Ballots.Count!=200 || v.Ballots.Select(b=>b.PersonId).Distinct().Count()!=200) throw new InvalidDataException("표결 기록 범위 오류");
        }
        command.Parameters.Clear(); command.CommandText=$"SELECT data FROM {table} WHERE run=$run AND id=$id";
        command.Parameters.AddWithValue("$run",run); command.Parameters.AddWithValue("$id",item.Id);
        if(command.ExecuteScalar() is string old && old!=item.Json) throw new InvalidDataException("인물 기록 ID 충돌");
        command.CommandText=$"INSERT INTO {table}(run,id,hour,data,hash) VALUES($run,$id,$hour,$data,$hash) ON CONFLICT(run,id) DO NOTHING";
        command.Parameters.AddWithValue("$hour",item.Hour); command.Parameters.AddWithValue("$data",item.Json); command.Parameters.AddWithValue("$hash",Hash(item.Json));
        command.ExecuteNonQuery();
    }
    public IReadOnlyList<PersonActivity> ReadActivities(GameState state,int maximum=80,long beforeId=long.MaxValue)
        => ReadWorld<PersonActivity>(state,"activities",maximum,beforeId).Concat(state.World!.Activities.Where(a=>a.Id<beforeId))
            .GroupBy(a=>a.Id).Select(g=>g.Last()).OrderByDescending(a=>a.Id).Take(Math.Clamp(maximum,1,1000)).ToList();
    public IReadOnlyList<WorldVote> ReadWorldVotes(GameState state,int maximum=60)
        => ReadWorld<WorldVote>(state,"world_votes",maximum,long.MaxValue).Concat(state.World!.Votes)
            .GroupBy(v=>v.Id).Select(g=>g.Last()).OrderByDescending(v=>v.Id).Take(Math.Clamp(maximum,1,1000)).ToList();
    List<T> ReadWorld<T>(GameState state,string table,int maximum,long before)
    {
        var result=new List<T>();
        if(File.Exists(SavePath))
        {
            using var connection=Open(SavePath,true); using var cmd=connection.CreateCommand();
            cmd.CommandText="SELECT count(*) FROM sqlite_master WHERE name=$name"; cmd.Parameters.AddWithValue("$name",table);
            if((long)cmd.ExecuteScalar()!>0)
            {
                cmd.Parameters.Clear(); cmd.CommandText=$"SELECT data,hash FROM {table} WHERE run=$run AND id<$before ORDER BY id DESC LIMIT $max";
                cmd.Parameters.AddWithValue("$run",state.RunId); cmd.Parameters.AddWithValue("$before",before); cmd.Parameters.AddWithValue("$max",Math.Clamp(maximum,1,1000));
                using var reader=cmd.ExecuteReader(); while(reader.Read())
                { string json=reader.GetString(0); if(Hash(json)!=reader.GetString(1)) throw new InvalidDataException("인물 기록 체크섬 오류"); result.Add(JsonSerializer.Deserialize<T>(json)!); }
            }
        }
        EventRecord[] queued; lock(pending) queued=(table=="activities" ? pendingActivities : pendingWorldVotes).GetValueOrDefault(state.RunId,[]).ToArray();
        result.AddRange(queued.Where(e=>e.Id<before).Select(e=>JsonSerializer.Deserialize<T>(e.Json)!)); return result;
    }
}
