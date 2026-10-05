using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    static readonly string[] SectorNames=["전체","기술","산업·에너지","바이오·헬스","금융","미디어","소비재"];
    string marketSector="전체";
    (AlphaExchange.Core.GameState State,string Sector,long Corporate,long Failure)? companyFilterKey;
    int[] allFiltered=[],listedFiltered=[];
    int[] FilteredCompanies(bool listedOnly=false)
    {
        var key=(S,marketSector,S.NextCorporateEventId,S.NextBankruptcyId);
        if(companyFilterKey!=key)
        {
            allFiltered=Enumerable.Range(0,S.Stocks.Count).Where(i=>marketSector=="전체" || S.Stocks[i].Sector==marketSector).ToArray();
            listedFiltered=allFiltered.Where(i=>S.Stocks[i].Active && !S.Stocks[i].WaitingForCapital).ToArray(); companyFilterKey=key;
        }
        return listedOnly ? listedFiltered : allFiltered;
    }
    float SectorTabs(float y)
    {
        for(int i=0;i<SectorNames.Length;i++)
        {
            string name=SectorNames[i]; bool active=marketSector==name;
            float x=20+i%4*92,top=y+i/4*36;
            Box(x,top,84,29,active ? Lime : Card2,9);
            Text(name,x+42,top+20,10,active ? Bg : Muted,active,Paint.Align.Center);
            Hit(x,top,84,29,()=>marketSector=name);
        }
        return y+83;
    }
    float CompanySelector(float y,int selected,Action<int> choose)
    {
        y=SectorTabs(y); int n=0;
        foreach(int index in FilteredCompanies())
        {
            var stock=S.Stocks[index]; float x=20+n%5*73,top=y+n/5*34; bool active=selected==index;
            Box(x,top,68,28,active ? Lime : Card2,8);
            Text(stock.Symbol,x+34,top+19,10,active ? Bg : Muted,active,Paint.Align.Center);
            Hit(x,top,68,28,()=>{ choose(index); scroll=0; }); n++;
        }
        return y+(int)Math.Ceiling(n/5.0)*34+15;
    }
}
