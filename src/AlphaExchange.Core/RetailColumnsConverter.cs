using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlphaExchange.Core;

// A column keeps its type, and identical values are written once. Sparse positions
// keep every nonzero cost/reservation as well as holdings. The old row format is
// still accepted, including existing v8 saves. This changes storage, not the ledger.
internal sealed class RetailColumnsConverter : JsonConverter<List<Trader>>
{
    const string Encoding = "retail-columns-1";
    static readonly Trader Defaults = new();
    static readonly HashSet<string> Excluded = ["Id", "IsRetail", "OpeningSnapshot", "SeasonSnapshot", "EquityHistory", "SeasonRanks", "Decision", "ActiveMethods", "LastAction"];
    static readonly IColumn[] Columns = typeof(Trader).GetProperties().Where(p=>p.GetCustomAttribute<JsonIgnoreAttribute>() is null && !Excluded.Contains(p.Name))
        .Select(CreateColumn).ToArray();
    static readonly Dictionary<string,IColumn> ByName = Columns.ToDictionary(c=>c.Name);
    static JsonElement Required(JsonElement value,string name)
        => value.ValueKind==JsonValueKind.Object && value.TryGetProperty(name,out var field) ? field : throw new JsonException("Missing retail storage field: "+name);

    interface IColumn
    {
        string Name { get; }
        void Write(Utf8JsonWriter writer,List<Trader> rows,JsonSerializerOptions options);
        void Read(JsonElement value,List<Trader> rows,JsonSerializerOptions options);
    }
    static IColumn CreateColumn(PropertyInfo p)
    {
        if(p.PropertyType==typeof(long[])) return new Positions<long>(p);
        if(p.PropertyType==typeof(double[])) return new Positions<double>(p);
        if(p.PropertyType==typeof(long)) return new Column<long>(p);
        if(p.PropertyType==typeof(int)) return new Column<int>(p);
        if(p.PropertyType==typeof(double)) return new Column<double>(p);
        if(p.PropertyType==typeof(bool)) return new Column<bool>(p);
        if(p.PropertyType==typeof(string)) return new Column<string>(p);
        if(p.PropertyType==typeof(Strategy)) return new Column<Strategy>(p);
        if(p.PropertyType==typeof(Disposition)) return new Column<Disposition>(p);
        if(p.PropertyType==typeof(Abilities)) return new Objects<Abilities>(p,a=>a.Values().Any(v=>v!=0));
        if(p.PropertyType==typeof(InstitutionDevelopment)) return new Objects<InstitutionDevelopment>(p,a=>a is not null);
        throw new InvalidOperationException("Unregistered retail storage field: "+p.Name);
    }
    static void Value<T>(Utf8JsonWriter writer,T value,JsonSerializerOptions options)
    {
        // No boxed/reflection property lookup in the 10,000-row numeric loops.
        if(typeof(T)==typeof(long)) writer.WriteNumberValue((long)(object)value!);
        else if(typeof(T)==typeof(int)) writer.WriteNumberValue((int)(object)value!);
        else if(typeof(T)==typeof(double)) writer.WriteNumberValue((double)(object)value!);
        else if(typeof(T)==typeof(bool)) writer.WriteBooleanValue((bool)(object)value!);
        else if(typeof(T)==typeof(string)) writer.WriteStringValue((string?)(object?)value);
        else JsonSerializer.Serialize(writer,value,options);
    }
    sealed class Column<T>(PropertyInfo p) : IColumn
    {
        readonly Func<Trader,T> get=p.GetMethod!.CreateDelegate<Func<Trader,T>>();
        readonly Action<Trader,T> set=p.SetMethod!.CreateDelegate<Action<Trader,T>>();
        public string Name=>p.Name;
        public void Write(Utf8JsonWriter writer,List<Trader> rows,JsonSerializerOptions options)
        {
            T first=get(rows[0]); bool uniform=true;
            for(int i=1;i<rows.Count;i++) if(!EqualityComparer<T>.Default.Equals(first,get(rows[i]))) { uniform=false; break; }
            if(uniform && EqualityComparer<T>.Default.Equals(first,get(Defaults))) return;
            writer.WritePropertyName(Name); writer.WriteStartArray();
            if(uniform) Value(writer,first,options);
            else for(int i=0;i<rows.Count;i++) Value(writer,get(rows[i]),options);
            writer.WriteEndArray();
        }
        public void Read(JsonElement value,List<Trader> rows,JsonSerializerOptions options)
        {
            if(value.ValueKind!=JsonValueKind.Array || value.GetArrayLength()!=1 && value.GetArrayLength()!=rows.Count)
                throw new JsonException("Invalid retail column length");
            if(value.GetArrayLength()==1)
            { T scalar=value[0].Deserialize<T>(options)!; foreach(var row in rows) set(row,scalar); }
            else { int i=0; foreach(var element in value.EnumerateArray()) set(rows[i++],element.Deserialize<T>(options)!); }
        }
    }
    sealed class Positions<T>(PropertyInfo p) : IColumn where T:unmanaged
    {
        readonly Func<Trader,T[]> get=p.GetMethod!.CreateDelegate<Func<Trader,T[]>>();
        readonly Action<Trader,T[]> set=p.SetMethod!.CreateDelegate<Action<Trader,T[]>>();
        public string Name=>p.Name;
        static bool Nonzero(T value)=>typeof(T)==typeof(double) ? BitConverter.DoubleToInt64Bits((double)(object)value)!=0 : !EqualityComparer<T>.Default.Equals(value,default);
        public void Write(Utf8JsonWriter writer,List<Trader> rows,JsonSerializerOptions options)
        {
            int length=0;
            foreach(var row in rows) { var a=get(row); if(MemoryMarshal.AsBytes(a.AsSpan()).ContainsAnyExcept((byte)0)) length=Math.Max(length,a.Length); }
            if(length==0) return;
            writer.WritePropertyName(Name); writer.WriteStartObject(); writer.WriteNumber("Length",length);
            writer.WritePropertyName("Values"); writer.WriteStartArray();
            for(int i=0;i<rows.Count;i++)
            { var a=get(rows[i]); for(int j=0;j<a.Length;j++) if(Nonzero(a[j])) { writer.WriteNumberValue(i); writer.WriteNumberValue(j); Value(writer,a[j],options); } }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        public void Read(JsonElement value,List<Trader> rows,JsonSerializerOptions options)
        {
            int length=Required(value,"Length").GetInt32(); var cells=Required(value,"Values");
            if(length is <1 or >2048 || cells.ValueKind!=JsonValueKind.Array || cells.GetArrayLength()%3!=0 || cells.GetArrayLength()>checked(rows.Count*length*3))
                throw new JsonException("Invalid retail positions");
            foreach(var row in rows) set(row,new T[length]);
            int previousRow=-1,previousStock=-1;
            for(int k=0;k<cells.GetArrayLength();k+=3)
            {
                int row=cells[k].GetInt32(),stock=cells[k+1].GetInt32();
                if(row<0 || row>=rows.Count || stock<0 || stock>=length || row<previousRow || row==previousRow && stock<=previousStock)
                    throw new JsonException("Duplicate or unordered retail position");
                get(rows[row])[stock]=cells[k+2].Deserialize<T>(options); previousRow=row; previousStock=stock;
            }
        }
    }
    sealed class Objects<T>(PropertyInfo p,Func<T,bool> present) : IColumn where T:class
    {
        readonly Func<Trader,T> get=p.GetMethod!.CreateDelegate<Func<Trader,T>>();
        readonly Action<Trader,T> set=p.SetMethod!.CreateDelegate<Action<Trader,T>>();
        public string Name=>p.Name;
        public void Write(Utf8JsonWriter writer,List<Trader> rows,JsonSerializerOptions options)
        {
            if(!rows.Any(r=>present(get(r)))) return;
            writer.WritePropertyName(Name); writer.WriteStartArray();
            for(int i=0;i<rows.Count;i++) if(present(get(rows[i])))
            { writer.WriteStartArray(); writer.WriteNumberValue(i); JsonSerializer.Serialize(writer,get(rows[i]),options); writer.WriteEndArray(); }
            writer.WriteEndArray();
        }
        public void Read(JsonElement value,List<Trader> rows,JsonSerializerOptions options)
        {
            if(value.ValueKind!=JsonValueKind.Array || value.GetArrayLength()>rows.Count) throw new JsonException("Invalid retail object rows");
            int previous=-1;
            foreach(var cell in value.EnumerateArray())
            {
                if(cell.ValueKind!=JsonValueKind.Array || cell.GetArrayLength()!=2) throw new JsonException("Invalid retail object");
                int index=cell[0].GetInt32(); if(index<=previous || index>=rows.Count) throw new JsonException("Invalid retail object index");
                set(rows[index],cell[1].Deserialize<T>(options) ?? throw new JsonException("Null retail object")); previous=index;
            }
        }
    }
    public override List<Trader> Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {
        if(reader.TokenType==JsonTokenType.StartArray) return JsonSerializer.Deserialize<List<Trader>>(ref reader,options)!;
        using var document=JsonDocument.ParseValue(ref reader); var root=document.RootElement;
        if(Required(root,"Encoding").GetString()!=Encoding) throw new JsonException("Unknown retail encoding");
        int count=Required(root,"Count").GetInt32(); if(count is <1 or >GameEngine.RetailCount) throw new JsonException("Invalid retail population");
        var rows=new List<Trader>(count);
        for(int i=0;i<count;i++) rows.Add(new Trader { Id=GameEngine.AiCount+i+1,IsRetail=true });
        var names=new HashSet<string>();
        foreach(var property in Required(root,"Columns").EnumerateObject())
        {
            if(!names.Add(property.Name) || !ByName.TryGetValue(property.Name,out var column)) throw new JsonException("Unknown or duplicate retail column");
            column.Read(property.Value,rows,options);
        }
        return rows;
    }
    public override void Write(Utf8JsonWriter writer,List<Trader> value,JsonSerializerOptions options)
    {
        if(value.Count==0 || value.Where((t,i)=>!t.IsRetail || t.Id!=GameEngine.AiCount+i+1).Any())
        { JsonSerializer.Serialize(writer,value,options); return; }
        writer.WriteStartObject(); writer.WriteString("Encoding",Encoding); writer.WriteNumber("Count",value.Count);
        writer.WritePropertyName("Columns"); writer.WriteStartObject();
        foreach(var column in Columns) column.Write(writer,value,options);
        writer.WriteEndObject(); writer.WriteEndObject();
    }
}
