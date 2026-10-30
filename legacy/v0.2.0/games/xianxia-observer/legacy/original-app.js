import React,{useEffect,useMemo,useRef,useState}from"react";
const RL=[{n:"연기경",ls:400},{n:"축기경",ls:800},{n:"금단경",ls:1600},{n:"원영경",ls:3200},{n:"화신경",ls:6400},{n:"합체경",ls:12800},{n:"도겁경",ls:25600},{n:"대승경",ls:51200},{n:"반선경",ls:102400}];
const MS=["초기","중기","후기","대원만"],BM=[[1,2,4,2],[5,10,20,10],[10,20,20,10],[20,30,50,100],[50,100,200,200],[100,200,400,500],[500,500,500,1000],[1000,1000,1000,5000],[5000,5000,5000,5000]];
const CVN=["무급","1성","2성","3성","4성","5성","6성","7성","8성","9성","현품","지품","천품","선품","성품","제품","황품","왕품","극품","신품"],CVMR=[0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,7,8,8,8];
const RRES={"종주":{ss:50,gw:1.6},"대장로":{ss:30,gw:1.45},"장로":{ss:20,gw:1.3},"직전제자":{ss:15,gw:1.2},"내문제자":{ss:10,gw:1.12},"외문제자":{ss:5,gw:1.06}};
const cvCost=function(cv){return cv<10?(cv+1)*300:(cv-8)*3000;};
const GL={1:"범재",2:"양호",3:"우수",4:"천재",5:"절세"},IL={1:"범품",2:"황품",3:"현품",4:"지품",5:"천품"};
const SR=["종주","대장로","장로","직전제자","내문제자","외문제자"],SL={"종주":1,"대장로":2,"장로":10,"직전제자":50,"내문제자":100,"외문제자":200};
const RC=["#6a6","#2a7","#15a","#70b","#b70","#b52","#c44","#83a","#860"],GC={1:"#888",2:"#2a7",3:"#15a",4:"#b70",5:"#b82"};
const DC={"체력고갈":"#b82","수명종료":"#666","돌파반동":"#70b","세력전":"#b52","부상누적":"#c43"};
const LC={"수련":"#15a","탐색":"#2a7","거래":"#640","전투":"#b82","비무":"#360","대비무":"#3a8","돌파":"#70b","윤회":"#83a","천겁":"#870","관계":"#a47","종문":"#850","세력":"#b52","부상":"#c43","전쟁":"#c60"};
const TC=[{max:100,sc:5000,lb:"1~100위",co:"#7a4800"},{max:300,sc:2000,lb:"101~300위",co:"#6a3a70"},{max:500,sc:800,lb:"301~500위",co:"#15a"},{max:700,sc:300,lb:"501~700위",co:"#2a7"},{max:1000,sc:100,lb:"701~1000위",co:"#888"}];
const IDEO={"정도":{bm:function(r){return r<4?1.0:r<6?1.3:1.5;},dc:"기초중시·고경지특화",co:"#15a"},"마도":{bm:function(r){return r<3?1.8:r<5?1.45:r<7?0.85:0.6;},dc:"빠른상승·고경지약화",co:"#c44"}};
const RGD=[{id:0,n:"청운 본산",tp:"중심지",si:1,au:200},{id:1,n:"청운 동원",tp:"주변",si:1,au:168},{id:2,n:"청운 남림",tp:"주변",si:1,au:162},{id:3,n:"청운 북봉",tp:"주변",si:1,au:158},{id:4,n:"천마 본관",tp:"중심지",si:2,au:195},{id:5,n:"천마 서원",tp:"주변",si:2,au:165},{id:6,n:"천마 남협",tp:"주변",si:2,au:160},{id:7,n:"천마 북호",tp:"주변",si:2,au:155},{id:8,n:"중원 평원",tp:"접경지",si:null,au:95},{id:9,n:"혈천 협곡",tp:"접경지",si:null,au:88},{id:10,n:"만령 산맥",tp:"접경지",si:null,au:85},{id:11,n:"황사 사막",tp:"접경지",si:null,au:82},{id:12,n:"옥룡 호수",tp:"접경지",si:null,au:90}];
const NA="청운진목하월현염설린풍연도백흑금옥소류명천혁비무신".split(""),NB="운하진월검영화천린명아호야도성우설풍란휘혁봉랑백기".split("");
const WAR_DUR=1000,WAR_CYC=11000;
function wPh(t){var p=t%WAR_CYC;return {war:p<WAR_DUR,rem:p<WAR_DUR?WAR_DUR-p:WAR_CYC-p};}
function r_(a,b){return Math.random()*(b-a)+a;}
function ri(a,b){return Math.floor(r_(a,b+1));}
function pk(a){return a[Math.floor(Math.random()*a.length)];}
function ch(p){return Math.random()<p;}
function cl(v,a,b){return Math.max(a,Math.min(b,v));}
function rn(c){return RL[c.ri].n+" "+MS[c.mi];}
function ft(c){return c.ws+c.ph+c.bn+c.rt;}
function cg(s){return s<600?1:s<1000?2:s<1400?3:s<1700?4:5;}
function GT(r){return TC.find(function(t){return r<=t.max;})||TC[4];}
function fN(n){var s=n<0?"-":"",a=Math.abs(n);return a>=1e8?s+(a/1e8).toFixed(1)+"억":a>=1e4?s+(a/1e4).toFixed(1)+"만":s+Math.floor(a);}
function fS(n){return Math.round(Math.abs(n)).toLocaleString();}
function aS(p){return (p.hr||0)*1000+(p.mi||0)*100+(p.bk||0)*10+(p.wn||0)*3+(p.st||0)*5;}
function mHp(c){return Math.max(50,Math.floor(30+c.ph/4+c.ri*35));}
function rY(r2,m2){return BM[r2][m2]*100;}
function sc2(r2){return [0,0,0,.6,.72,.83,.9,.95,.98][cl(r2,0,8)];}
function gFac(f){
  var base=40/2000;
  var raw=(f/2000)*Math.pow(Math.max(1,f)/40,0.3);
  return base+(raw-base)*5;
}
function tBns(f){return Math.round((Math.pow(Math.max(40,f)/40,1.3)-1)*100*5);}
function smc(s){return SR.reduce(function(n,rk){return n+s.ro[rk].length;},0);}
var HD={};
function uhd(c){
  if(!HD[c.id])HD[c.id]={id:c.id,nm:c.nm,bS:0,bHr:0,bMi:0,bDr:"없음",allS:[],t5:0,tBk:0,tWn:0,tSt:0,rc:0,lDr:"없음"};
  var d=HD[c.id],cs=aS({hr:c.la2.hr,mi:c.ri,bk:c.la2.bk||0,wn:c.la2.wn||0,st:c.la2.st||0});
  d.nm=c.nm;d.tBk+=c.la2.bk||0;d.tWn+=c.la2.wn||0;d.tSt+=c.la2.st||0;d.rc=c.rc;d.lDr=c.dr||"수명종료";
  if(cs>d.bS){d.bS=cs;d.bHr=c.la2.hr;d.bMi=c.ri;d.bDr=d.lDr;}
  d.allS.push(cs);d.allS.sort(function(a,b){return b-a;});if(d.allS.length>50)d.allS.length=50;
  d.t5=d.allS.slice(0,5).reduce(function(s,v){return s+v;},0);
}
function uRc(ag){ag.slice().sort(function(a,b){return b.rc-a.rc;}).forEach(function(a,i){a.rcS=GT(i+1).sc;a.rcR=i+1;});}
function rta(boost,best){
  var bb=best&&best.sc>1000?Math.floor(Math.log10(best.sc)*10):0,t=(boost||0)+bb;
  function sv(){return cl(ri(10+Math.floor(t*.3),500),10,500);}
  var w=sv(),p=sv(),b=sv(),o=sv();return {ws:w,ph:p,bn:b,rt:o,gr:cg(w+p+b+o)};
}
function tfa(a,best){return rta(a.hr*8+(a.fs?30:0)+a.st*5+Math.min(25,(a.tw||0)*3),best);}
function ig(){var x=Math.random();return x<.38?1:x<.68?2:x<.86?3:x<.96?4:5;}
function cpb(c,sc){
  var f=ft(c),rf=(c.ri+1)*10000+c.mi*2000;
  var tfBase=500,tfRaw=Math.round(Math.pow(f/40,1.3)*500),tf=tfBase+(tfRaw-tfBase)*5;
  var cvf=(c.cv+1)*400;
  var ef=Math.floor(c.ag*80+c.mg*40+c.pg*30);
  var bf=Math.floor((c.bx||0)*15+(c.wn||0)*10+c.bk*20+(c.rcS||100)+(c.ctrib||0)*.5+c.cb*3);
  var vf=Math.floor(Math.min(2000,c.af*20+c.hb*2+c.ss*.02));
  var ndiLen=(c.ndi||[]).length;
  var lf=c.ei.length*4+ndiLen*8+(c.dw?15:0);
  var sf=c.si!=null&&sc&&sc.some(function(s){return s.id===c.si&&s.ac;})?100:0;
  var ip=(c.inj||0)*-200;
  return {rf:rf,tf:tf,cvf:cvf,ef:ef,bf:bf,vf:vf,lf:lf,sf:sf,ip:ip,tot:Math.max(1,rf+tf+cvf+ef+bf+vf+lf+sf+ip)};
}
function cp(c,sc){return cpb(c,sc).tot;}
function reRankRoster(W,sm,tick){
  for(var i=0;i<W.sc.length;i++){
    var s=W.sc[i];
    var members=[];
    for(var j=0;j<W.ag.length;j++){var a=W.ag[j];if(a.al&&a.si===s.id)members.push(a);}
    if(!members.length){s.ac=false;continue;}
    members.sort(function(a,b){return (b.ctrib||0)-(a.ctrib||0)||b.ri-a.ri||b.mi-a.mi;});
    SR.forEach(function(rk){s.ro[rk]=[];});
    var slots=[{rk:"종주",max:1},{rk:"대장로",max:2},{rk:"장로",max:10},{rk:"직전제자",max:50},{rk:"내문제자",max:100},{rk:"외문제자",max:200}];
    var prevFd=s.fd;
    for(var m=0;m<members.length;m++){
      var ag2=members[m];
      for(var k=0;k<slots.length;k++){
        if(s.ro[slots[k].rk].length<slots[k].max){s.ro[slots[k].rk].push(ag2.id);ag2.sr=slots[k].rk;break;}
      }
    }
    if(s.ro["종주"].length>0){
      var nfd=sm[s.ro["종주"][0]];
      if(nfd){s.fd=nfd.id;if(nfd.id!==prevFd&&prevFd)W.lg.push({tick:tick,tp:"종문",tx:"["+s.nm+"] "+nfd.nm+" 종주 등극."});}
    }
    s.ac=true;
  }
}
function removeFromRoster(a,s){SR.forEach(function(rk){s.ro[rk]=s.ro[rk].filter(function(x){return x!==a.id;});});}
function fillSlot(a,s){
  var order=["외문제자","내문제자","직전제자","장로","대장로","종주"];
  for(var i=0;i<order.length;i++){var rk=order[i];if(s.ro[rk].length<SL[rk]){s.ro[rk].push(a.id);a.si=s.id;a.sr=rk;return true;}}
  a.si=s.id;a.sr=null;return false;
}
function cc(id){
  var t=rta(0,null);
  return {id:id,nm:pk(NA)+pk(NB)+"자",ws:t.ws,ph:t.ph,bn:t.bn,rt:t.rt,gr:t.gr,pe:pk(["정파","마도","중립","은둔","패도"]),am:Math.random(),cr:Math.random(),lo:Math.random(),ca:Math.random(),ch:Math.random(),ri:0,mi:0,yen:0,el:0,cv:ri(0,2),age:ri(16,40),ls:400,ss:ri(30,140),hb:ri(0,10),af:ri(0,2),mg:ig(),pg:ig(),ag:ig(),cb:ri(8,20),rc:0,al:true,bk:0,wn:0,lo2:0,bx:0,inj:0,dw:null,nmd:null,ndi:[],ei:[],si:null,sr:null,re:0,lt:0,la:"탄생",la2:{hr:0,fs:false,st:0,tw:0,wn:0,bk:0},rm:[],rh:[],best:{sc:0},rcS:100,rcR:500,ctrib:0,lastSi:null};
}
function cr2(){return RGD.map(function(d){return {id:d.id,n:d.n,tp:d.tp,si:d.si,au:d.au,ctrl:d.si||null,pts:0,wc:0,lw:"평온"};});}
function cw(){
  HD={};
  var ag=Array.from({length:1000},function(_,i){return cc(i+1);}),rg=cr2();
  function mkS(id,nm,ideo){var ro={};SR.forEach(function(rk){ro[rk]=[];});return {id:id,nm:nm,fd:null,su:null,ac:true,ideo:ideo,contrib:0,ro:ro};}
  var s1=mkS(1,"청운종","정도"),s2=mkS(2,"천마문","마도");
  ag.slice(0,500).forEach(function(a,i){a.lastSi=1;a.re=i<300?ri(0,3):ri(8,12);fillSlot(a,s1);});
  ag.slice(500).forEach(function(a,i){a.lastSi=2;a.re=i<300?ri(4,7):ri(8,12);fillSlot(a,s2);});
  if(!s1.ro["종주"].length){s1.ro["외문제자"]=s1.ro["외문제자"].filter(function(x){return x!==ag[0].id;});s1.ro["종주"]=[ag[0].id];ag[0].sr="종주";}
  if(!s2.ro["종주"].length){s2.ro["외문제자"]=s2.ro["외문제자"].filter(function(x){return x!==ag[500].id;});s2.ro["종주"]=[ag[500].id];ag[500].sr="종주";}
  s1.fd=s1.ro["종주"][0];s2.fd=s2.ro["종주"][0];
  return {ag:ag,lg:[],sc:[s1,s2],rg:rg,ds:{"수명종료":0},tb:0,bimu:0};
}
function kc(c,R,tick,lg,ds,sc){
  c.al=false;c.dr=R;c.la2.hr=Math.max(c.la2.hr,c.ri);
  var s=sc.find(function(x){return x.id===c.si;});if(s)removeFromRoster(c,s);
  if(c.nmd!=null&&lg._ag){for(var i=0;i<lg._ag.length;i++){if(lg._ag[i].id===c.nmd){var m=lg._ag[i];if(m.ndi)m.ndi=m.ndi.filter(function(x){return x!==c.id;});break;}}}
  if(c.ndi&&c.ndi.length>0&&lg._ag){for(var j=0;j<c.ndi.length;j++){for(var k=0;k<lg._ag.length;k++){if(lg._ag[k].id===c.ndi[j]){lg._ag[k].nmd=null;break;}}}}
  c.lastSi=c.si;c.si=null;c.sr=null;
  ds[R]=(ds[R]||0)+1;lg.push({tick:tick,tp:"윤회",tx:c.nm+" ["+R+"] 사망."});
}
function rnc(c,lg,tick){
  uhd(c);
  var pr=c.rc,cs=aS({hr:c.la2.hr,mi:c.ri,bk:c.la2.bk||0,wn:c.la2.wn||0,st:c.la2.st||0});
  var pl={ln:c.rc+1,hr:c.la2.hr,mi:c.ri,dr:c.dr||"수명종료",da:c.age,wn:c.la2.wn||0,bk:c.la2.bk||0};
  var h=(c.rh||[]).concat([pl]).slice(-20),nb=cs>c.best.sc?{hr:c.la2.hr,mi:c.ri,sc:cs}:c.best,pSi=c.lastSi;
  var newRe=pSi===1?ri(0,3):pSi===2?ri(4,7):ri(8,12);
  Object.assign(c,tfa(c.la2,nb),{ri:0,mi:0,yen:0,el:ri(0,50),cv:ri(0,1),age:16,ls:400,ss:ri(30,140),hb:ri(0,10),af:ri(0,2),mg:ig(),pg:ig(),ag:ig(),cb:ri(8,20),rc:pr+1,al:true,dr:undefined,wn:0,lo2:0,bx:0,inj:0,si:pSi,sr:null,re:newRe,la:"윤회",la2:{hr:0,fs:false,st:0,tw:0,wn:0,bk:0},rm:[],rh:h,best:nb,dw:null,nmd:null,ndi:[],ei:[],ctrib:0,lastSi:pSi});
  if(pSi!=null){var sx=lg._sc&&lg._sc.find(function(s){return s.id===pSi;});if(sx)fillSlot(c,sx);}
  lg.push({tick:tick,tp:"윤회",tx:c.nm+" 윤회 ("+c.rc+"회)"});
}
// 소경지 하락: 체력 소진 시
function realmDropS(c,tick,lg){
  c.inj=0;c.yen*=.5;c.cb=Math.max(1,c.cb-ri(3,8));
  var prev=rn(c);
  if(c.mi>0)c.mi--;
  else if(c.ri>0){c.ri--;c.mi=3;c.ls=RL[c.ri].ls;}
  lg.unshift({tick:tick,tp:"부상",tx:"[소경지하락] "+c.nm+" "+prev+"->"+rn(c)});
}
// 대경지 하락: 치명타 / 돌파반동 시
function realmDropB(c,tick,lg){
  c.inj=0;c.yen=0;c.cb=Math.max(1,c.cb-ri(10,25));
  var prev=rn(c);
  if(c.ri>0){c.ri--;c.mi=0;c.ls=RL[c.ri].ls;}
  else c.mi=0;
  lg.unshift({tick:tick,tp:"부상",tx:"[대경지하락] "+c.nm+" "+prev+"->"+rn(c)});
}
function atrib(c,tick,lg,ds,sc){
  if(!c.al||c.ri<6)return;
  var p=cl(.18+(ft(c)+c.cb+(c.mg+c.ag+c.pg)*12+(c.cv+1)*20)/1200+c.rc*.01-c.ri*.04,.08,.9);
  c.lt=tick;
  if(ch(p)){c.cb+=ri(6,16);c.yen+=ri(100,300);c.la2.st++;lg.unshift({tick:tick,tp:"천겁",tx:c.nm+" 천겁 통과!"});}
  else{var pv=rn(c);if(c.mi>0)c.mi--;else if(c.ri>0){c.ri--;c.mi=3;c.ls=RL[c.ri].ls;}c.yen*=.25;c.cb=Math.max(1,c.cb-ri(6,18));lg.unshift({tick:tick,tp:"천겁",tx:c.nm+" 천겁 실패 "+pv+"->"+rn(c)});}
}
function mbk(c,tick,lg,ds,sc,am){
  if(!c.al)return;var req=rY(c.ri,c.mi);if(c.yen<req)return;
  // 공법 한계 초과 시 확률 감소 (제한 삭제, 대신 패널티)
  var cvGap=c.ri-CVMR[c.cv];// 0이면 한계, 양수면 초과
  var cvPenalty=cvGap>=1?cl(1-(0.5+Math.min(cvGap-1,2)*0.245),0.01,0.5):1.0;
  var f=ft(c),eB=cl((c.el||0)/1000*.3,0,.3),eb=c.ri<=4?.24:c.ri<=6?.1:0,rm2=c.ri<=4?2:c.ri<=6?1.1:c.ri<=7?1.08:.92;
  var ndBonus=c.nmd!=null?1.15:1.0;
  var p=cl((gFac(f)*0.25+c.rc*.012+c.mg*.025+c.pg*.025+eb)*rm2*ndBonus+eB-(c.mi===3?(c.ri<=4?.02:.08):0)-(c.age>c.ls*.75?.06:0),.05,.995);
  var sect=sc&&c.si?sc.find(function(s){return s.id===c.si;}):null;
  if(sect)p=cl(p*1.12,.05,.995);
  // 경지별 돌파 배율
  var isBig=c.mi===3;
  var riMult=c.ri===0?5.0:c.ri<=2?3.0:c.ri<=4?2.0:c.ri<=6?1.5:(c.ri===7&&isBig)?0.8:1.5;
  p=cl(p*riMult*cvPenalty,.005,.995);
  c.yen-=req;
  if(ch(p)){
    var wb=c.mi===3,pv=c.ri;if(c.mi<3)c.mi++;else if(c.ri<8){c.ri++;c.mi=0;c.ls=RL[c.ri].ls;}
    c.la2.hr=Math.max(c.la2.hr,c.ri);c.bk++;c.la2.bk=(c.la2.bk||0)+1;c.cb+=ri(3,10);c.el=0;if(c.inj>0)c.inj--;
    if(c.ri!==pv)c.rm=(c.rm||[]).concat([{ri:c.ri,age:c.age}]);
    var tribGain=(c.ri+1)*100+c.bk*10;c.ctrib=(c.ctrib||0)+tribGain;
    if(sect)sect.contrib=(sect.contrib||0)+tribGain;
    if(c.nmd&&am){var mst=am[c.nmd];if(mst&&mst.al){mst.ctrib=(mst.ctrib||0)+Math.floor(tribGain*.5);var ms2=sc&&mst.si?sc.find(function(s){return s.id===mst.si;}):null;if(ms2)ms2.contrib=(ms2.contrib||0)+Math.floor(tribGain*.5);}}
    lg.unshift({tick:tick,tp:"돌파",tx:c.nm+" "+rn(c)+" 돌파! 공헌+"+tribGain});
    if(wb&&c.ri>=6)atrib(c,tick,lg,ds,sc);
  }else if(c.mi===3){
    c.inj=(c.inj||0)+1;c.mi=2;lg.unshift({tick:tick,tp:"돌파",tx:c.nm+" 대경지 돌파 실패!"});
  }else if(ch(.018+c.ri*.003)){
    // 돌파반동 → 대경지 하락 (모든 경지 사망 없음)
    realmDropB(c,tick,lg);
    lg.unshift({tick:tick,tp:"돌파",tx:c.nm+" 돌파반동! 대경지 하락."});
  }else c.yen*=.2;
}
function doBimu(a,b,tick,W,cross){
  var ap=cp(a,W.sc)*r_(.8,1.2),dp=cp(b,W.sc)*r_(.8,1.2),w=ap>=dp?a:b,l=ap>=dp?b:a,g=ri(5,20);
  w.ss+=g;l.ss=Math.max(0,l.ss-g);w.wn++;l.lo2++;w.bx++;l.bx++;if(cross)W.bimu=(W.bimu||0)+1;
  if(ch(.2))W.lg.unshift({tick:tick,tp:cross?"대비무":"비무",tx:(cross?"[대비무]":"[비무]")+" "+w.nm+" 승리."});
}
function fight(lo,wi,tick,W,fatal){
  lo.lo2++;wi.wn++;wi.bx++;lo.bx++;wi.la2.wn=(wi.la2.wn||0)+1;
  var ss=Math.floor(lo.ss*.4),hb2=Math.floor(lo.hb*.35);
  lo.ss-=ss;wi.ss+=ss;lo.hb-=hb2;wi.hb+=hb2;wi.yen+=Math.floor(25+ft(lo)*.06);lo.yen*=.25;lo.cb=Math.max(3,lo.cb-ri(5,15));
  wi.ctrib=(wi.ctrib||0)+50;var ws2=W.sc.find(function(s){return s.id===wi.si;});if(ws2)ws2.contrib=(ws2.contrib||0)+50;
  var mx=mHp(lo);
  if(fatal){
    // 치명타 → 대경지 하락 + 전리품 일부
    var loot=Math.floor(lo.ss*.3);wi.ss+=loot;lo.ss-=loot;
    realmDropB(lo,tick,W.lg);
    W.lg.unshift({tick:tick,tp:"전투",tx:wi.nm+"이 "+lo.nm+"에게 대경지하락을 입혔다. (영석+"+loot+")"});
  }else{
    lo.inj=(lo.inj||0)+1;
    if(lo.inj>=mx){
      // 체력 고갈 → 소경지 하락 + 전리품 일부
      var loot2=Math.floor(lo.ss*.2);wi.ss+=loot2;lo.ss-=loot2;
      realmDropS(lo,tick,W.lg);
    }else{
      W.lg.unshift({tick:tick,tp:"부상",tx:lo.nm+" 부상 "+lo.inj+"/"+mx});
    }
  }
}
function rcb(a,b,tick,W,isW){
  if(!a.al||!b.al)return;W.tb=(W.tb||0)+1;
  if(a.si!==null&&a.si===b.si){doBimu(a,b,tick,W,false);return;}
  if(!wPh(tick).war){doBimu(a,b,tick,W,true);return;}
  var inBorder=W.rg[a.re]&&W.rg[a.re].tp==="접경지";
  if(!inBorder&&!isW){doBimu(a,b,tick,W,false);return;}
  var ap=cp(a,W.sc)*r_(.82,1.18)+a.am*25+(a.ei.indexOf(b.id)>=0?40:0),dp=cp(b,W.sc)*r_(.82,1.18)+b.ca*18;
  var lo=ap>=dp?b:a,wi=ap>=dp?a:b;
  fight(lo,wi,tick,W,isW||(lo.ri>2&&!ch(sc2(lo.ri))));
}
function warTick(W,tick){
  var sc=W.sc;if(sc.length<2)return;var sid1=sc[0].id,sid2=sc[1].id;
  for(var ri3=0;ri3<W.rg.length;ri3++){
    var rg=W.rg[ri3];if(rg.tp!=="접경지"||!ch(.45))continue;
    var w1=[],w2=[];
    for(var j=0;j<W.ag.length;j++){var a=W.ag[j];if(!a.al||a.ri<1)continue;if(a.si===sid1&&(a.re===rg.id||ch(.2)))w1.push(a);else if(a.si===sid2&&(a.re===rg.id||ch(.2)))w2.push(a);}
    if(!w1.length||!w2.length)continue;
    var fa=pk(w1),fb=pk(w2);rcb(fa,fb,tick,W,true);
    var wi=W.ag.find(function(x){return (x.id===fa.id||x.id===fb.id)&&x.al;});if(!wi)continue;
    rg.pts=cl((rg.pts||0)+(wi.si===sid1?1:-1),-10,10);
    var nc=rg.pts>=4?sid1:rg.pts<=-4?sid2:rg.ctrl;
    if(nc!==rg.ctrl){rg.ctrl=nc;var sn=nc?sc.find(function(s){return s.id===nc;}):null;rg.lw=(sn?sn.nm:"중립")+" 점령";W.lg.push({tick:tick,tp:"전쟁",tx:"["+rg.n+"] "+(sn?sn.nm:"중립")+" 지배!"});}
    rg.wc++;
    if(rg.ctrl){for(var k=0;k<W.ag.length;k++){var a2=W.ag[k];if(a2.al&&a2.si===rg.ctrl&&a2.re===rg.id)a2.ss+=ri(1,3);}}
  }
}
function peaceBimu(W,tick){
  if(!ch(.3)||W.sc.length<2)return;
  var p1=[],p2=[];for(var i=0;i<W.ag.length;i++){var a=W.ag[i];if(!a.al||a.ri<1)continue;if(a.si===W.sc[0].id)p1.push(a);else if(a.si===W.sc[1].id)p2.push(a);}
  if(p1.length&&p2.length)doBimu(pk(p1),pk(p2),tick,W,true);
}
function bIdx(W){
  var am={},rb={};for(var i=0;i<W.ag.length;i++){var a=W.ag[i];am[a.id]=a;if(a.al){if(!rb[a.re])rb[a.re]=[];rb[a.re].push(a);}}
  var sm={};for(var j=0;j<W.sc.length;j++){sm[W.sc[j].id]=W.sc[j];}return {am:am,rb:rb,sm:sm};
}
function simTick(W,tick,idx){
  var ag=W.ag,sc=W.sc,lg=W.lg,rg=W.rg,ds=W.ds,am=idx.am,rb=idx.rb,sm=idx.sm;lg._sc=sc;lg._ag=ag;
  if(tick%100===0)uRc(ag);
  if(tick%500===0)reRankRoster(W,sm,tick);
  for(var i=0;i<ag.length;i++){
    var c=ag[i];if(!c.al)continue;
    c.age++;c.la2.hr=Math.max(c.la2.hr,c.ri);
    if(c.age>=c.ls){kc(c,"수명종료",tick,lg,ds,sc);rnc(c,lg,tick);continue;}
    if(c.ri>=6&&tick-c.lt>=1000)atrib(c,tick,lg,ds,sc);
    if(!c.al){rnc(c,lg,tick);continue;}
    if(c.inj>0&&c.age%80===0)c.inj--;
    // 기명 제자 형성
    if(c.ri>=3&&(c.ndi||[]).length<3&&ch(.005)){
      var cands=[];for(var ci=0;ci<ag.length;ci++){var oc=ag[ci];if(oc.al&&oc.id!==c.id&&oc.nmd==null&&oc.ri<=c.ri-2&&oc.ri>=1&&(c.si==null||oc.si===c.si))cands.push(oc);}
      if(cands.length){var nd=pk(cands);if(!c.ndi)c.ndi=[];c.ndi.push(nd.id);nd.nmd=c.id;lg.push({tick:tick,tp:"종문",tx:c.nm+"이(가) "+nd.nm+"을(를) 기명 제자로 받아들였다."});}
    }
    // 종문 가입
    if(!c.si){var jn=sc.filter(function(s){return s.ac;});if(jn.length&&ch(.05)){var js=pk(jn);if(fillSlot(c,js))lg.push({tick:tick,tp:"종문",tx:c.nm+" ["+js.nm+"] 입문."});}}
    // 경매/공법
    if(ch(.07)&&c.hb>0&&ch(.3)){c.hb--;c.ss+=ri(24,60)+c.pg*3;}
    else if(ch(.04)&&c.ss>=50){var cost=cvCost(c.cv);if(c.cv<19&&c.ss>=cost&&c.ri>=CVMR[c.cv]&&ch(.05)){c.ss-=cost;c.cv++;lg.unshift({tick:tick,tp:"종문",tx:c.nm+" "+CVN[c.cv]+" 공법 획득!"});}else{var tg=pk(["mg","pg","ag"]),c2=ri(50,130);if(c.ss>=c2){c.ss-=c2;c[tg]=cl(c[tg]+(ch(.45)?1:0),1,5);}}}
    // 직위별 자원 지급
    if(c.si&&c.sr&&tick%100===0){var rres=RRES[c.sr];if(rres)c.ss+=ri(Math.floor(rres.ss*.5),rres.ss);}
    // 수행
    var ndGwMul=c.nmd!=null?1.2:1.0;
    var curRg=rg[c.re],ctB=curRg&&curRg.tp==="접경지"&&curRg.ctrl===c.si?1.3:1,hmB=c.si&&curRg&&curRg.si===c.si?1.15:1;
    var rkgw=c.sr&&RRES[c.sr]?RRES[c.sr].gw:1.0;
    var gw2=gFac(ft(c))*cl((curRg?curRg.au:100)/100,.1,1.5)*(c.cv+1)/20*60*ctB*hmB*ndGwMul*rkgw;
    var rl=Math.random();
    if(rl<.4){c.yen+=gw2*r_(.9,1.8);c.el=cl(c.el+cl(gFac(ft(c))*r_(.3,1.5)*.5,.05,3),0,1000);c.la="수련";}
    else if(rl<.55){c.ss+=ri(8,34)+Math.floor(c.ch*6);if(ch(.4+c.ph/5200))c.hb++;if(ch(.03+c.gr*.014))c.af++;c.yen+=gw2*.5;c.la="탐색";}
    else if(rl<.68){var ot=rb[c.re]||[];if(ot.length>1){var o2=ot[Math.floor(Math.random()*ot.length)];if(o2&&o2.id!==c.id&&c.hb>0){var pr=ri(14,44);if(o2.ss>=pr){c.hb--;c.ss+=pr;o2.hb++;o2.ss-=pr;}}}c.la="거래";}
    else{var cds=(rb[c.re]||[]).filter(function(x){return x.id!==c.id&&Math.abs(x.ri-c.ri)<=2;});if(cds.length){var tgt=cds[Math.floor(Math.random()*cds.length)];rcb(c,tgt,tick,W,false);if(!c.al){rnc(c,lg,tick);continue;}if(!tgt.al)rnc(tgt,lg,tick);}}
    mbk(c,tick,lg,ds,sc,am);if(!c.al)rnc(c,lg,tick);
  }
  if(wPh(tick).war)warTick(W,tick);else peaceBimu(W,tick);
  for(var zi=0;zi<ag.length;zi++)if(!ag[zi].al)rnc(ag[zi],lg,tick);
  if(lg.length>400)lg.splice(0,lg.length-360);
}
function snap(W){return {ag:W.ag.slice(),lg:W.lg.slice().reverse().slice(0,360),sc:W.sc.slice(),rg:W.rg.slice(),ds:Object.assign({},W.ds),tb:W.tb,bimu:W.bimu};}
var CSS=[".x{font-family:'Noto Serif KR',serif;background:#f5ede0;color:#1e0e00;min-height:100vh;max-width:480px;margin:0 auto;padding-bottom:60px}",
  ".xp{background:#ede3cc;border:1px solid rgba(100,65,20,.22);border-radius:10px}",
  ".xc{background:#e4d8bc;border:1px solid rgba(100,65,20,.2);border-radius:8px}",
  ".xb{padding:5px 9px;border-radius:7px;font-size:11px;cursor:pointer;border:1px solid rgba(100,65,20,.25);background:transparent;color:#1e0e00;font-family:inherit;transition:all .15s}",
  ".xb.a{background:#7a4800;color:#fdf0d8;font-weight:700;border-color:#7a4800}",
  ".xnav{position:fixed;bottom:0;left:50%;transform:translateX(-50%);width:100%;max-width:480px;background:#e8d8b8;border-top:1px solid rgba(100,65,20,.25);display:flex;z-index:200}",
  ".xnt{flex:1;padding:7px 0;display:flex;flex-direction:column;align-items:center;gap:1px;cursor:pointer;font-family:inherit;background:transparent;border:none;color:#8a6840;font-size:9px}",
  ".xnt.a{color:#7a4800;font-weight:700}",
  ".xbg{position:fixed;inset:0;background:rgba(40,20,5,.85);z-index:300;overflow-y:auto;padding:10px}",
  ".xm{background:#ede3cc;border:2px solid #7a4800;border-radius:12px;padding:14px;max-width:460px;margin:0 auto}",
  "@keyframes gl{0%,100%{box-shadow:none}50%{box-shadow:0 0 12px 3px rgba(122,72,0,.2)}}",
  ".xg{animation:gl 3s infinite}"].join("");
var e=React.createElement;
function B(p){return e("div",{style:{height:p.h||4,background:"rgba(100,65,20,.12)",borderRadius:3,overflow:"hidden"}},e("div",{style:{width:cl(p.p,0,100)+"%",height:p.h||4,background:p.c||"#7a4800",borderRadius:3}}));}
function Bdg(p){return e("span",{style:{display:"inline-flex",alignItems:"center",padding:"1px 6px",borderRadius:999,fontSize:9,fontWeight:700,background:p.bg||"rgba(122,72,0,.1)",color:p.co||"#7a4800",border:"1px solid "+(p.br||"rgba(122,72,0,.3)"),whiteSpace:"nowrap"}},p.v);}
function KV(p){return e("div",{style:{display:"flex",justifyContent:"space-between",padding:"3px 0",borderBottom:"1px solid rgba(100,65,20,.1)",fontSize:11}},e("span",{style:{color:"#8a6840"}},p.lb),e("span",{style:{fontWeight:600,color:p.co||"#1e0e00"}},p.vl));}
function Modal(props){
  var c=props.c,sc=props.sc,ag=props.ag,onClose=props.onClose,onAgent=props.onAgent;
  var tabSt=useState("현재"),tab=tabSt[0],setTab=tabSt[1];
  if(!c)return null;
  // 모든 변수를 최상단에 선언
  var hd=HD[c.id];
  var sect=null;for(var si=0;si<sc.length;si++){if(sc[si].id===c.si){sect=sc[si];break;}}
  var mx=mHp(c),bd=cpb(c,sc),f=ft(c);
  var pct=Math.min(100,Math.floor((c.yen/rY(c.ri,c.mi))*100));
  var rT=GT(c.rcR||500);
  var idio=sect?IDEO[sect.ideo]:null;
  var hiR=c.ri>=4;
  var talPct=tBns(f),gw3=Math.round(gFac(f)*(c.cv+1)/20*60);
  var ideoBns=idio?Math.round((idio.bm(c.ri)-1)*100):0;
  var rkRes=c.sr?RRES[c.sr]:null;
  var nmaster=null;
  if(c.nmd){for(var ni=0;ni<ag.length;ni++){if(ag[ni].id===c.nmd){nmaster=ag[ni];break;}}}
  var ndisciples=[];
  if(c.ndi){for(var nj=0;nj<c.ndi.length;nj++){for(var nk=0;nk<ag.length;nk++){if(ag[nk].id===c.ndi[nj]){ndisciples.push(ag[nk]);break;}}}}

  function body(){
    if(tab==="현재"){
      // 기명 제자/스승 섹션 미리 빌드
      var relItems=[];
      if(nmaster){
        var mId=nmaster.id;
        relItems.push(e("div",{key:"ms",style:{display:"flex",justifyContent:"space-between",fontSize:10,marginBottom:3}},
          e("span",{style:{color:"#8a6840"}},"스승"),
          e("span",{style:{fontWeight:700,color:"#7a4800",cursor:"pointer"},onClick:function(){onAgent(mId);}},nmaster.nm+" ("+rn(nmaster)+")")));
      }
      if(ndisciples.length>0){
        var discNodes=ndisciples.map(function(nd){
          var ndId=nd.id;
          return e("div",{key:nd.id,style:{display:"flex",justifyContent:"space-between",fontSize:10,padding:"2px 0",cursor:"pointer"},onClick:function(){onAgent(ndId);}},
            e("span",{style:{fontWeight:700}},nd.nm),
            e("span",{style:{color:"#8a6840"}},rn(nd)));
        });
        relItems.push(e("div",{key:"ndlist"},
          e("div",{style:{fontSize:9,color:"#8a6840",marginBottom:2}},"기명 제자 ("+ndisciples.length+"/3)"),
          discNodes));
      }
      var relSection=relItems.length>0?e("div",{style:{background:"rgba(122,72,0,.07)",borderRadius:8,padding:"8px 10px",marginBottom:8}},relItems):null;
      var srBdg=c.sr?e(Bdg,{v:c.sr,bg:"rgba(122,72,0,.12)",co:"#7a4800"}):null;
      return e("div",null,
        e("div",{style:{background:"linear-gradient(135deg,rgba(122,72,0,.15),rgba(122,72,0,.05))",border:"1px solid rgba(122,72,0,.3)",borderRadius:9,padding:"11px",marginBottom:8,textAlign:"center"}},
          e("div",{style:{fontSize:18,fontWeight:700,color:RC[c.ri]||"#7a4800"}},rn(c)),
          e("div",{style:{fontSize:10,color:"#8a6840",marginTop:2}},GL[c.gr]+" · "+c.pe+" · "+CVN[c.cv]+(sect?" · ["+sect.nm+"]("+sect.ideo+")":"")),
          hiR?e("div",{style:{fontSize:9,color:"#15a",fontWeight:700,marginTop:2}},"[모든 경지] 수명 소진만 사망 · 전투시 경지 하락"):null,
          e("div",{style:{display:"flex",gap:3,justifyContent:"center",flexWrap:"wrap",marginTop:5}},
            e(Bdg,{v:"나이 "+c.age+"/"+c.ls}),
            e(Bdg,{v:"체력 "+(mx-(c.inj||0))+"/"+mx,co:(c.inj||0)===0?"#2a7":"#b82"}),
            e(Bdg,{v:"윤회 "+c.rc+"회",bg:"rgba(100,60,140,.12)",co:"#70a",br:"rgba(100,60,140,.3)"}),
            srBdg)),
        e("div",{style:{marginBottom:8}},
          e(B,{p:pct,c:"linear-gradient(90deg,#7a480060,#7a4800)",h:7}),
          e("div",{style:{display:"flex",justifyContent:"space-between",fontSize:9,color:"#8a6840",marginTop:2}},
            e("span","영기 "+fN(c.yen)+" / "+fN(rY(c.ri,c.mi))),e("span",pct+"%"))),
        e("div",{style:{display:"grid",gridTemplateColumns:"repeat(3,1fr)",gap:4,marginBottom:8}},
          [["영석",fN(c.ss),"#b70"],["영초",c.hb,"#2a7"],["영전",c.af,"#15a"],["깨달음",Math.round(c.el||0)+"/1000","#70b"],["내공",c.cb,"#850"],["공헌점",fS(c.ctrib||0),"#c60"]].map(function(row){
            return e("div",{key:row[0],style:{background:"rgba(30,14,0,.06)",borderRadius:6,padding:"5px 7px",textAlign:"center"}},
              e("div",{style:{fontSize:9,color:"#8a6840"}},row[0]),
              e("div",{style:{fontWeight:700,fontSize:12,color:row[2]||"#1e0e00"}},row[1]));
          })),
        relSection,
        e("div",{style:{background:"rgba(122,72,0,.07)",borderRadius:8,padding:"8px 10px"}},
          e("div",{style:{fontSize:10,fontWeight:700,color:"#7a4800",marginBottom:5}},"재능 "+f+"/2000  (+"+talPct+"%)"),
          [["오성(x8)",c.ws],["체질(x10)",c.ph],["근골(x6)",c.bn],["영근(x8)",c.rt]].map(function(row){
            return e("div",{key:row[0],style:{marginBottom:4}},
              e("div",{style:{display:"flex",justifyContent:"space-between",fontSize:10,marginBottom:1}},e("span",{style:{color:"#8a6840"}},row[0]),e("span",null,row[1]+"/500")),
              e(B,{p:row[1]/5,c:"hsl("+(40+Math.floor(row[1]/12))+",60%,40%)"}));
          })));
    }
    if(tab==="전투력"){
      return e("div",null,
        e("div",{style:{background:"rgba(30,14,0,.07)",borderRadius:9,padding:"10px 12px",marginBottom:8}},
          e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:6}},
            e("span",{style:{fontWeight:700,color:"#7a4800"}},"총 전투력"),
            e("span",{style:{fontWeight:700,fontSize:20,color:"#7a4800"}},fN(bd.tot))),
          [["경지(x5배)",bd.rf,"#70b"],["재능(지수보정)",bd.tf,"#2a7"],["공법",bd.cvf,"#b70"],["장비",bd.ef,"#b82"],["전투경험+공헌",bd.bf,"#b40"],["자원",bd.vf,"#15a"],["종문소속",bd.sf,"#850"],["부상패널티",bd.ip,"#c44"]].map(function(row){
            return row[1]!==0?e("div",{key:row[0],style:{marginBottom:4}},
              e("div",{style:{display:"flex",justifyContent:"space-between",fontSize:10,marginBottom:1}},e("span",{style:{color:"#8a6840"}},row[0]),e("span",{style:{color:row[2],fontWeight:700}},(row[1]>0?"+":"")+fN(row[1]))),
              e(B,{p:Math.abs(row[1])/Math.max(1,bd.tot)*100,c:row[2],h:3})):null;
          })));
    }
  if(tab==="성장"){
      return e("div",null,
        e("div",{style:{background:"rgba(122,72,0,.08)",borderRadius:9,padding:"10px 12px",marginBottom:8}},
          e("div",{style:{fontWeight:700,color:"#7a4800",marginBottom:6}},"성장 속도 분석"),
          [["자질배율",gFac(f).toFixed(3)+"배 (+"+talPct+"%)"],["공법계수","x"+((c.cv+1)/20).toFixed(2)+" ("+CVN[c.cv]+")"],["직위보너스",rkRes?"x"+rkRes.gw+" ("+c.sr+")":"없음"],["기명제자보너스",c.nmd!=null?"x1.2":"없음"],["기본 영기/년",fN(gw3)],["종문 소속 보너스",sect?"+12% (동일 적용)":"없음"],["깨달음보너스","+"+Math.round((c.el||0)/1000*.3*100)+"%"]].map(function(row){return e(KV,{key:row[0],lb:row[0],vl:row[1]});})),
        e("div",{style:{background:"rgba(30,14,0,.06)",borderRadius:8,padding:"8px 10px",marginBottom:8}},
          e("div",{style:{fontWeight:700,color:"#7a4800",marginBottom:4}},"직위별 자원 (100틱마다)"),
          SR.map(function(rk){var res=RRES[rk];return e(KV,{key:rk,lb:rk,vl:"영석+"+res.ss+" / 수행x"+res.gw,co:c.sr===rk?"#7a4800":"#8a6840"});})),
        e("div",{style:{background:"rgba(184,28,28,.08)",borderRadius:8,padding:"8px 10px"}},
          e("div",{style:{fontWeight:700,color:"#b82",marginBottom:4}},"전투 패널티 (사망 없음 - 수명만)"),
          [["체력 고갈","소경지 하락 + 영석 20% 상실"],["치명타 피격","대경지 하락 + 영석 30% 상실"],["돌파반동","대경지 하락 + 영기 전소"],["수명 소진","유일한 사망 → 윤회"],["최대 체력",mx+"점 (체질 "+c.ph+" 기반)"],["나이/수명",c.age+"/"+c.ls+"세"]].map(function(row){return e(KV,{key:row[0],lb:row[0],vl:row[1]});})
        ));
    }
    if(tab==="공법"){
      return e("div",null,
        e("div",{style:{background:"rgba(122,72,0,.08)",borderRadius:9,padding:"11px",marginBottom:8}},
          e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:6}},
            e("div",null,
              e("div",{style:{fontSize:14,fontWeight:700,color:"#7a4800"}},CVN[c.cv]+" 공법"),
              e("div",{style:{fontSize:10,color:"#8a6840"}},"경지한계: "+RL[CVMR[c.cv]].n+(idio?" · "+sect.ideo+"("+idio.dc+")":" "))),
            e("div",{style:{fontSize:20}},"**")),
          e(B,{p:(c.cv/19)*100,c:"#7a4800",h:5}),
          e("div",{style:{display:"flex",justifyContent:"space-between",fontSize:9,color:"#8a6840",marginTop:4}},e("span","업그레이드: "+fS(cvCost(c.cv))+"영석"),e("span",c.cv+"/19")),
          c.ri>=CVMR[c.cv]&&c.mi===3?e("div",{style:{fontSize:10,color:"#b82",fontWeight:700,marginTop:4}},"[한계] 상위 공법 필요!"):null),
        e("div",{style:{borderRadius:8,background:"rgba(122,72,0,.06)",padding:"8px 10px"}},
          [["비급",c.mg,120],["단약",c.pg,100],["법보",c.ag,250]].map(function(row){return e(KV,{key:row[0],lb:row[0]+" ("+IL[row[1]]+")",vl:"+"+fN(row[1]*row[2])+" 전투력",co:GC[row[1]]});})));
    }
    if(tab==="역대"){
      if(!hd)return e("div",{style:{color:"#8a6840",textAlign:"center",padding:"20px 0"}},"윤회 이력 없음");
      return e("div",{style:{background:"rgba(100,60,140,.1)",border:"1px solid rgba(100,60,140,.2)",borderRadius:9,padding:"10px 12px"}},
        e("div",{style:{fontWeight:700,color:"#70a",marginBottom:6}},"역대 합산 ("+hd.rc+"회 윤회)"),
        [["최고 경지",RL[hd.bHr].n+" "+MS[hd.bMi]],["최고 업적",fS(hd.bS)],["5개 합산",fS(hd.t5)],["합산 돌파",hd.tBk+"회"],["합산 승리",hd.tWn+"회"],["합산 천겁",hd.tSt+"회"],["최근 사인",hd.lDr]].map(function(row){return e(KV,{key:row[0],lb:row[0],vl:row[1]});}));
    }
    if(tab==="윤회"){
      var rm3=c.rm||[],rh3=(c.rh||[]).slice(-5).reverse();
      return e("div",null,
        rm3.length>0?e("div",{style:{marginBottom:8}},
          e("div",{style:{fontSize:10,fontWeight:700,color:"#7a4800",marginBottom:4}},"현생 이정표"),
          rm3.map(function(m,i){return e("div",{key:i,style:{fontSize:10,color:RC[m.ri]||"#7a4800",padding:"2px 0"}},RL[m.ri].n+" · "+m.age+"세");})):null,
        e("div",{style:{background:"rgba(100,60,140,.06)",borderRadius:9,padding:"10px 12px"}},
          e("div",{style:{fontSize:10,fontWeight:700,color:"#7a4800",marginBottom:4}},"최근 5회 윤회"),
          rh3.map(function(lf,i){var dc2=DC[lf.dr]||"#888",rc2=RC[lf.hr]||"#888";return e("div",{key:i,style:{background:"rgba(30,14,0,.04)",borderRadius:6,padding:"5px 8px",marginBottom:3,borderLeft:"3px solid "+dc2}},e("div",{style:{display:"flex",gap:4,flexWrap:"wrap",fontSize:10}},e("span",{style:{fontWeight:700,color:"#70a"}},lf.ln+"생"),e(Bdg,{v:RL[lf.hr].n,bg:rc2+"18",co:rc2,br:rc2+"40"}),e(Bdg,{v:lf.dr,bg:dc2+"18",co:dc2,br:dc2+"35"}),e("span",{style:{color:"#8a6840"}},lf.da+"세")));})));
    }
    return null;
  }
  return e("div",{className:"xbg",onClick:onClose},
    e("div",{className:"xm",onClick:function(ev){ev.stopPropagation();}},
      e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"flex-start",marginBottom:8}},
        e("div",null,e("div",{style:{fontSize:16,fontWeight:700,color:"#7a4800"}},c.nm),e("div",{style:{fontSize:10,color:"#8a6840"}},rn(c)+(sect?" · ["+sect.nm+"] "+c.sr:"")+" · "+c.pe)),
        e("button",{onClick:onClose,style:{background:"transparent",border:"none",color:"#8a6840",fontSize:22,cursor:"pointer",lineHeight:1}},"x")),
      e("div",{style:{display:"flex",gap:2,overflowX:"auto",marginBottom:9,paddingBottom:2}},
        ["현재","전투력","성장","공법","역대","윤회"].map(function(t){return e("button",{key:t,className:"xb"+(tab===t?" a":""),style:{padding:"4px 0",fontSize:10,flex:"0 0 auto",minWidth:48,textAlign:"center"},onClick:function(){setTab(t);}},t);})),
      body()));
}
function SectModal(props){
  var sect=props.sect,ag=props.ag,sc=props.sc,rg=props.rg,onClose=props.onClose,onAgent=props.onAgent;
  var tabSt=useState("정보"),tab=tabSt[0],setTab=tabSt[1];
  if(!sect)return null;
  var idio=IDEO[sect.ideo],ico=idio?idio.co:"#7a4800";
  var members=ag.filter(function(a){return a.al&&a.si===sect.id;});
  var fd=null;for(var fi=0;fi<ag.length;fi++){if(ag[fi].id===sect.fd){fd=ag[fi];break;}}
  var totalP=members.reduce(function(s,a){return s+cp(a,sc);},0);
  var avgRi=members.length?members.reduce(function(s,a){return s+a.ri;},0)/members.length:0;
  var topPow=members.slice().sort(function(a,b){return cp(b,sc)-cp(a,sc);}).slice(0,10);
  var topCtrib=members.slice().sort(function(a,b){return (b.ctrib||0)-(a.ctrib||0);}).slice(0,10);
  var riDist=RL.map(function(_,i){return members.filter(function(a){return a.ri===i;}).length;});
  var ctrlRgCount=rg.filter(function(r){return r.ctrl===sect.id;}).length;
  function body(){
    if(tab==="정보"){
      return e("div",null,
        e("div",{style:{background:ico+"12",border:"1px solid "+ico+"40",borderRadius:9,padding:"10px",marginBottom:8}},
          e("div",{style:{fontWeight:700,color:ico,fontSize:12,marginBottom:3}},sect.ideo+" 이념 · "+(idio?idio.dc:""))),
        e("div",{style:{background:"rgba(30,14,0,.06)",borderRadius:8,padding:"8px 10px",marginBottom:8}},
          [["구성원",members.length+"명"],["종주",fd?fd.nm:"공석"],["총전투력",fN(Math.floor(totalP))],["누적공헌",fS(sect.contrib||0)],["평균경지",RL[Math.min(8,Math.floor(avgRi))].n],["점령영토",ctrlRgCount+"개"]].map(function(row){return e(KV,{key:row[0],lb:row[0],vl:row[1]});})),
        e("div",{style:{background:"rgba(30,14,0,.06)",borderRadius:8,padding:"8px 10px",marginBottom:8}},
          e("div",{style:{fontSize:10,fontWeight:700,color:"#7a4800",marginBottom:5}},"직위 현황"),
          SR.map(function(rk){var cnt=sect.ro[rk].length,max=SL[rk];return e("div",{key:rk,style:{display:"flex",justifyContent:"space-between",fontSize:10,padding:"2px 0",borderBottom:"1px solid rgba(100,65,20,.07)"}},e("span",{style:{color:"#8a6840"}},rk),e("span",{style:{fontWeight:600,color:cnt>=max?"#b82":"#7a4800"}},cnt+"/"+max));})),
        e("div",{style:{background:"rgba(30,14,0,.06)",borderRadius:8,padding:"8px 10px"}},
          e("div",{style:{fontSize:10,fontWeight:700,color:"#7a4800",marginBottom:5}},"경지 분포"),
          RL.map(function(_,i){var cnt=riDist[i];if(!cnt)return null;return e("div",{key:i,style:{marginBottom:3}},e("div",{style:{display:"flex",justifyContent:"space-between",fontSize:9,marginBottom:1}},e("span",{style:{color:RC[i]||"#7a4800"}},RL[i].n),e("span",cnt+"명")),e(B,{p:(cnt/Math.max(1,members.length))*100,c:RC[i]||"#7a4800",h:4}));})));
    }
    if(tab==="강자"){
      return e("div",null,
        e("div",{style:{fontSize:10,color:"#8a6840",marginBottom:6}},"전투력 기준 상위 10인"),
        topPow.map(function(a,i){var aId=a.id;return e("div",{key:a.id,className:"xc",style:{padding:"7px 10px",marginBottom:5,cursor:"pointer"},onClick:function(){onAgent(aId);}},
          e("div",{style:{display:"flex",alignItems:"center",gap:6}},
            e("span",{style:{width:20,height:20,borderRadius:"50%",fontSize:9,fontWeight:700,display:"inline-flex",alignItems:"center",justifyContent:"center",background:i<3?"#7a4800":"rgba(100,65,20,.15)",color:i<3?"#fdf0d8":"#8a6840",flexShrink:0}},i+1),
            e("div",{style:{flex:1}},e("div",{style:{fontWeight:700,fontSize:11}},a.nm),e("div",{style:{fontSize:9,color:"#8a6840"}},rn(a)+" · "+(a.sr||"무직위")+" · 공헌 "+fS(a.ctrib||0))),
            e("div",{style:{fontWeight:700,fontSize:11,color:"#7a4800",flexShrink:0}},fN(cp(a,sc)))));}));
    }
    if(tab==="공헌"){
      return e("div",null,
        e("div",{style:{fontSize:10,color:"#8a6840",marginBottom:6}},"공헌점 기준 상위 10인 (500틱마다 직위 재편)"),
        topCtrib.map(function(a,i){var aId=a.id;return e("div",{key:a.id,className:"xc",style:{padding:"7px 10px",marginBottom:5,cursor:"pointer"},onClick:function(){onAgent(aId);}},
          e("div",{style:{display:"flex",alignItems:"center",gap:6}},
            e("span",{style:{width:20,height:20,borderRadius:"50%",fontSize:9,fontWeight:700,display:"inline-flex",alignItems:"center",justifyContent:"center",background:i<3?"#7a4800":"rgba(100,65,20,.15)",color:i<3?"#fdf0d8":"#8a6840",flexShrink:0}},i+1),
            e("div",{style:{flex:1}},e("div",{style:{fontWeight:700,fontSize:11}},a.nm),e("div",{style:{fontSize:9,color:"#8a6840"}},rn(a)+" · "+(a.sr||"무직위"))),
            e("div",{style:{fontWeight:700,fontSize:11,color:"#c60",flexShrink:0}},fS(a.ctrib||0))));}));
    }
    return null;
  }
  return e("div",{className:"xbg",onClick:onClose},
    e("div",{className:"xm",onClick:function(ev){ev.stopPropagation();}},
      e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"flex-start",marginBottom:8}},
        e("div",null,e("div",{style:{fontSize:16,fontWeight:700,color:ico}},sect.nm),e("div",{style:{fontSize:10,color:"#8a6840"}},sect.ideo+" · "+members.length+"명 / 등록 "+smc(sect)+"명")),
        e("button",{onClick:onClose,style:{background:"transparent",border:"none",color:"#8a6840",fontSize:22,cursor:"pointer",lineHeight:1}},"x")),
      e("div",{style:{display:"flex",gap:3,marginBottom:9}},["정보","강자","공헌"].map(function(t){return e("button",{key:t,className:"xb"+(tab===t?" a":""),style:{flex:1,padding:"4px 0",fontSize:10,textAlign:"center"},onClick:function(){setTab(t);}},t);})),
      body()));
}
export default function App(){
  var wRef=useRef(null);if(!wRef.current)wRef.current=cw();
  var iRef=useRef(null);
  var WS=useState(function(){return snap(wRef.current);}),W=WS[0],setW=WS[1];
  var tS=useState(0),tick=tS[0],setTick=tS[1];
  var rS=useState(true),run=rS[0],setRun=rS[1];
  var bS=useState(5),batch=bS[0],setBatch=bS[1];
  var tabS=useState("전쟁"),tab=tabS[0],setTab=tabS[1];
  var lfS=useState("전체"),lf=lfS[0],setLf=lfS[1];
  var selS=useState(null),sel=selS[0],setSel=selS[1];
  var ssS=useState(null),sectSel=ssS[0],setSectSel=ssS[1];
  var aTS=useState("역대최고"),aTab=aTS[0],setATab=aTS[1];
  var rTS=useState("경지"),rType=rTS[0],setRType=rTS[1];
  var rPS=useState(0),rPage=rPS[0],setRPage=rPS[1];
  var ftES=useState({0:true,1:false,2:false,3:false,4:false}),ftExp=ftES[0],setFtExp=ftES[1];
  var aPgS=useState({"역대최고":0,"5개합":0}),aPg=aPgS[0],setAPg=aPgS[1];
  var tr=useRef(0);
  useEffect(function(){
    if(!run)return;
    var id=setInterval(function(){
      var W2=wRef.current;if(!iRef.current)iRef.current=bIdx(W2);
      for(var i=0;i<batch;i++){tr.current++;simTick(W2,tr.current,iRef.current);}
      iRef.current=bIdx(W2);setTick(tr.current);setW(snap(W2));
    },16);return function(){clearInterval(id);};
  },[run,batch]);
  function rst(){wRef.current=cw();iRef.current=null;tr.current=0;setTick(0);setSel(null);setSectSel(null);setW(snap(wRef.current));}
  var selC=sel!=null?W.ag.find(function(a){return a.id===sel;}):null;
  var selSect=sectSel!=null?W.sc.find(function(s){return s.id===sectSel;}):null;
  var achR=useMemo(function(){
    var vals=Object.values(HD);if(!vals.length)return {byBest:[],byTop5:[],combined:[]};
    var byBest=vals.slice().sort(function(a,b){return b.bS-a.bS;}),byTop5=vals.slice().sort(function(a,b){return b.t5-a.t5;});
    var bR={},t5R={};byBest.forEach(function(d,i){bR[d.id]=i+1;});byTop5.forEach(function(d,i){t5R[d.id]=i+1;});
    var combined=vals.map(function(d){return Object.assign({},d,{r1:bR[d.id]||9999,r2:t5R[d.id]||9999,cr:(bR[d.id]||9999)+(t5R[d.id]||9999)});});
    combined.sort(function(a,b){return a.cr-b.cr||b.bS-a.bS;});return {byBest:byBest,byTop5:byTop5,combined:combined};
  },[W]);
  var srt=useMemo(function(){return W.ag.slice().sort(function(a,b){return b.ri-a.ri||b.mi-a.mi||cp(b,W.sc)-cp(a,W.sc);});},[W]),ldr=srt[0];
  var injN=W.ag.filter(function(a){return (a.inj||0)>0;}).length,PS=20;
  function RBG(r){return r<=3?"#7a4800":r<=10?"rgba(90,58,24,.6)":"rgba(100,65,20,.15)";}
  function RTX(r){return r<=3?"#fdf0d8":r<=10?"#f0d890":"#8a6840";}
  function AgCard(c,idx){
    var pct=Math.min(100,Math.floor((c.yen/rY(c.ri,c.mi))*100));
    var mv=rType==="경지"?rn(c):rType==="전투력"?fN(cp(c,W.sc)):rType==="나이"?fN(c.age)+"세":ft(c)+"/2000";
    var mc=rType==="경지"?"#70b":rType==="전투력"?"#7a4800":rType==="나이"?"#15a":"#2a7";
    var snNm="";for(var si=0;si<W.sc.length;si++){if(W.sc[si].id===c.si){snNm=" ["+W.sc[si].nm+"]";break;}}
    var cId=c.id;
    return e("div",{key:c.id,className:"xc"+(c.ri>=6?" xg":""),style:{padding:"8px 10px",marginBottom:5,cursor:"pointer"},onClick:function(){setSel(sel===cId?null:cId);}},
      e("div",{style:{display:"flex",alignItems:"center",gap:5,marginBottom:4}},
        e("span",{style:{width:22,height:22,borderRadius:"50%",fontSize:10,fontWeight:700,display:"inline-flex",alignItems:"center",justifyContent:"center",background:RBG(idx+1),color:RTX(idx+1),flexShrink:0}},idx+1),
        e("div",{style:{flex:1}},
          e("div",{style:{fontWeight:700,fontSize:12}},c.nm+snNm+(c.ri>=4?" *":"")),
          e("div",{style:{fontSize:9,color:"#8a6840",marginTop:1}},(rType!=="경지"?rn(c)+" · ":"")+CVN[c.cv]+" · "+(c.sr||"무직위")+" · 공헌 "+fS(c.ctrib||0))),
        e("div",{style:{fontSize:12,fontWeight:700,color:mc,flexShrink:0}},mv)),
      e(B,{p:pct,c:"linear-gradient(90deg,#7a480060,#7a4800)"}));
  }
  function AchCard(d,rank,t5){
    var ag2=W.ag.find(function(a){return a.id===d.id;}),rc2=RC[d.bHr]||"#888",dc=DC[d.bDr]||"#888";
    var dId=d.id;
    return e("div",{key:d.id,className:"xc",style:{padding:"8px 10px",marginBottom:5,cursor:"pointer"},onClick:function(){if(ag2)setSel(ag2.id);}},
      e("div",{style:{display:"flex",alignItems:"center",gap:5}},
        e("span",{style:{width:22,height:22,borderRadius:"50%",fontSize:10,fontWeight:700,display:"inline-flex",alignItems:"center",justifyContent:"center",background:RBG(rank),color:RTX(rank),flexShrink:0}},rank),
        e("div",{style:{flex:1}},
          e("div",{style:{fontWeight:700,fontSize:12}},d.nm+(ag2?" *":"")),
          e("div",{style:{display:"flex",gap:3,flexWrap:"wrap",marginTop:2}},
            e(Bdg,{v:RL[d.bHr].n,bg:rc2+"18",co:rc2,br:rc2+"40"}),
            e(Bdg,{v:"윤회 "+d.rc+"회",bg:"rgba(100,60,140,.12)",co:"#70a",br:"rgba(100,60,140,.3)"}),
            e(Bdg,{v:d.bDr,bg:dc+"18",co:dc,br:dc+"35"}))),
        e("div",{style:{textAlign:"right",flexShrink:0}},
          e("div",{style:{fontSize:12,fontWeight:700,color:"#7a4800"}},fS(t5?d.t5:d.bS)),
          e("div",{style:{fontSize:9,color:"#8a6840"}},t5?"5개합":"최고점수"))));
  }
  function PgCtrl(list,key,t5){
    var pg=aPg[key]||0,tp2=Math.max(1,Math.floor(list.length/PS)),sp=Math.min(pg,tp2-1),items=list.slice(sp*PS,(sp+1)*PS);
    return e("div",null,
      e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:5}},
        e("span",{style:{fontSize:10,color:"#8a6840"}},list.length+"명"),
        e("div",{style:{display:"flex",gap:3}},
          e("button",{className:"xb",style:{padding:"2px 7px",fontSize:10},onClick:function(){setAPg(function(x){var n=Object.assign({},x);n[key]=Math.max(0,sp-1);return n;});},disabled:sp===0},"<"),
          e("span",{style:{fontSize:10,padding:"2px 4px",color:"#8a6840"}},(sp+1)+"/"+tp2),
          e("button",{className:"xb",style:{padding:"2px 7px",fontSize:10},onClick:function(){setAPg(function(x){var n=Object.assign({},x);n[key]=Math.min(tp2-1,sp+1);return n;});},disabled:sp>=tp2-1},">"))),
      items.map(function(d,i){return AchCard(d,sp*PS+i+1,t5);}));
  }
  function renderWar(){
    var ph=wPh(tick),s1=W.sc[0],s2=W.sc[1];if(!s1||!s2)return null;
    var borders=W.rg.filter(function(r){return r.tp==="접경지";});
    var s1c=borders.filter(function(r){return r.ctrl===s1.id;}).length,s2c=borders.filter(function(r){return r.ctrl===s2.id;}).length;
    var ic1=IDEO[s1.ideo]?IDEO[s1.ideo].co:"#15a",ic2=IDEO[s2.ideo]?IDEO[s2.ideo].co:"#c44";
    var s1id=s1.id,s2id=s2.id;
    return e("div",{style:{padding:"0 10px 10px"}},
      e("div",{className:"xp",style:{padding:"10px 12px",marginBottom:8,marginTop:8,textAlign:"center",borderColor:ph.war?"#c60":"#2a7",background:ph.war?"rgba(200,60,0,.07)":"rgba(0,100,50,.05)"}},
        e("div",{style:{fontSize:14,fontWeight:700,color:ph.war?"#c60":"#2a7",marginBottom:3}},ph.war?"전쟁 중":"휴전 / 대비무 기간"),
        e("div",{style:{fontSize:10,color:"#8a6840"}},(ph.war?"종료까지 ":"다음 전쟁까지 ")+fN(ph.rem)+"년  (천년전쟁/만년휴전)")),
      e("div",{style:{display:"grid",gridTemplateColumns:"1fr auto 1fr",gap:6,alignItems:"center",marginBottom:8}},
        e("div",{style:{textAlign:"center",padding:"8px",background:ic1+"10",borderRadius:8,border:"1px solid "+ic1+"30",cursor:"pointer"},onClick:function(){setSectSel(s1id);}},
          e("div",{style:{fontWeight:700,fontSize:13,color:ic1}},s1.nm+" >"),
          e("div",{style:{fontSize:9,color:"#8a6840",marginBottom:3}},s1.ideo),
          e(Bdg,{v:"점령 "+s1c+"개",bg:ic1+"20",co:ic1,br:ic1+"40"}),
          e("div",{style:{fontSize:10,fontWeight:700,color:"#c60",marginTop:3}},"공헌 "+fS(s1.contrib||0))),
        e("div",{style:{fontSize:18,color:"#c44",fontWeight:700,textAlign:"center"}},"VS"),
        e("div",{style:{textAlign:"center",padding:"8px",background:ic2+"10",borderRadius:8,border:"1px solid "+ic2+"30",cursor:"pointer"},onClick:function(){setSectSel(s2id);}},
          e("div",{style:{fontWeight:700,fontSize:13,color:ic2}},s2.nm+" >"),
          e("div",{style:{fontSize:9,color:"#8a6840",marginBottom:3}},s2.ideo),
          e(Bdg,{v:"점령 "+s2c+"개",bg:ic2+"20",co:ic2,br:ic2+"40"}),
          e("div",{style:{fontSize:10,fontWeight:700,color:"#c60",marginTop:3}},"공헌 "+fS(s2.contrib||0)))),
      e("div",{className:"xp",style:{padding:"10px 12px",marginBottom:8}},
        e("div",{style:{fontWeight:700,color:"#7a4800",marginBottom:6}},"접경지 세력도 (+-4점 점령)"),
        borders.map(function(rg){
          var p=rg.pts||0,co=rg.ctrl===s1.id?ic1:rg.ctrl===s2.id?ic2:"#888";
          var ow=rg.ctrl===s1.id?s1.nm:rg.ctrl===s2.id?s2.nm:"중립";
          var barLeft=p>=0?"50%":"auto",barRight=p<0?"50%":"auto",barW=Math.abs(p)/10*50+"%";
          return e("div",{key:rg.id,style:{marginBottom:7}},
            e("div",{style:{display:"flex",justifyContent:"space-between",fontSize:10,marginBottom:2}},
              e("span",{style:{fontWeight:700}},rg.n),e("span",{style:{color:co,fontWeight:700}},ow),e("span",{style:{color:"#8a6840"}},"전투 "+rg.wc+"회")),
            e("div",{style:{height:7,background:"rgba(100,65,20,.12)",borderRadius:4,overflow:"hidden",position:"relative"}},
              e("div",{style:{position:"absolute",left:"50%",top:0,bottom:0,width:2,background:"rgba(100,65,20,.3)"}}),
              p!==0?e("div",{style:{height:7,width:barW,background:co,position:"absolute",left:barLeft,right:barRight}}):null));
        })),
      e("div",{className:"xp",style:{padding:"10px 12px"}},
        e("div",{style:{fontWeight:700,color:"#7a4800",marginBottom:6}},"13개 지역 현황"),
        ["중심지","주변","접경지"].map(function(tp){
          return e("div",{key:tp,style:{marginBottom:6}},
            e("div",{style:{fontSize:10,fontWeight:700,color:tp==="접경지"?"#c60":tp==="중심지"?"#b70":"#2a7",marginBottom:3}},tp+(tp==="접경지"?" (전쟁구역)":"")),
            W.rg.filter(function(r){return r.tp===tp;}).map(function(rg){
              var res=W.ag.filter(function(a){return a.re===rg.id;}).length;
              var ctrlS=null;for(var si=0;si<W.sc.length;si++){if(W.sc[si].id===rg.ctrl){ctrlS=W.sc[si];break;}}
              var co=ctrlS?(ctrlS.id===s1.id?ic1:ic2):"#888";
              return e("div",{key:rg.id,className:"xc",style:{padding:"6px 9px",marginBottom:3,borderLeft:"3px solid "+co}},
                e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"center"}},
                  e("span",{style:{fontWeight:700,fontSize:10}},rg.n),
                  e("div",{style:{display:"flex",gap:3}},
                    e(Bdg,{v:"영기 "+rg.au}),
                    e(Bdg,{v:res+"명",bg:"rgba(26,106,32,.1)",co:"#2a7",br:"rgba(26,106,32,.25)"}),
                    tp==="접경지"?e(Bdg,{v:ctrlS?ctrlS.nm:"중립",bg:co+"18",co:co,br:co+"40"}):null)));
            }));
        })));
  }
  function renderSect(){
    return e("div",{style:{padding:"0 10px 10px"}},
      W.sc.map(function(sect){
        var idio=IDEO[sect.ideo],ico=idio?idio.co:"#7a4800";
        var fd=null;for(var fi=0;fi<W.ag.length;fi++){if(W.ag[fi].id===sect.fd){fd=W.ag[fi];break;}}
        var members=W.ag.filter(function(a){return a.si===sect.id&&a.al;});
        var totalP=members.reduce(function(s,a){return s+cp(a,W.sc);},0);
        var sId=sect.id;
        return e("div",{key:sect.id,className:"xc",style:{padding:"10px 12px",marginBottom:8,borderLeft:"3px solid "+ico,cursor:"pointer"},onClick:function(){setSectSel(sId);}},
          e("div",{style:{display:"flex",justifyContent:"space-between",marginBottom:6}},
            e("div",null,e("div",{style:{fontSize:14,fontWeight:700,color:ico}},sect.nm+" >"),e("div",{style:{fontSize:9,color:"#8a6840"}},sect.ideo+(idio?" / "+idio.dc:""))),
            e("div",{style:{textAlign:"right"}},e("div",{style:{fontSize:14,fontWeight:700,color:ico}},members.length+" (등록 "+smc(sect)+")"),e("div",{style:{fontSize:9,color:"#8a6840"}},"구성원 / 직위 등록"))),
          e("div",{style:{display:"grid",gridTemplateColumns:"repeat(3,1fr)",gap:4,marginBottom:6}},
            [["종주",fd?fd.nm:"공석"],["총전투력",fN(Math.floor(totalP))],["공헌",fS(sect.contrib||0)]].map(function(row){return e("div",{key:row[0],style:{background:"rgba(30,14,0,.07)",borderRadius:5,padding:"4px 6px",textAlign:"center"}},e("div",{style:{fontSize:8,color:"#8a6840"}},row[0]),e("div",{style:{fontWeight:600,fontSize:10}},row[1]));})),
          e("div",{style:{display:"flex",flexWrap:"wrap",gap:3}},SR.map(function(rk){return e("div",{key:rk,style:{background:"rgba(122,72,0,.08)",borderRadius:4,padding:"2px 5px",fontSize:9,color:sect.ro[rk].length>=SL[rk]?"#b82":"#7a4800"}},rk+": "+sect.ro[rk].length+"/"+SL[rk]);})));
      }),
      e("div",{className:"xp",style:{padding:"10px 12px",marginTop:8}},
        e("div",{style:{fontSize:11,fontWeight:700,color:"#b82",marginBottom:8}},"사망 원인 통계"),
        Object.keys(W.ds).map(function(d){var tot=Object.values(W.ds).reduce(function(s,v){return s+v;},0),dc=DC[d]||"#b82";return e(KV,{key:d,lb:d,vl:W.ds[d]+"회 ("+(tot>0?Math.round(W.ds[d]/tot*100):0)+"%)",co:dc});})));
  }
  function renderRanking(){
    var ranked=W.ag.slice().sort(rType==="경지"?function(a,b){return b.ri-a.ri||b.mi-a.mi||b.age-a.age;}:rType==="전투력"?function(a,b){return cp(b,W.sc)-cp(a,W.sc);}:rType==="나이"?function(a,b){return b.age-a.age;}:function(a,b){return ft(b)-ft(a);});
    var tp=Math.max(1,Math.floor(ranked.length/PS)),sp=Math.min(rPage,tp-1);
    return e("div",{style:{padding:"0 10px 10px"}},
      e("div",{style:{display:"flex",gap:3,paddingTop:8,marginBottom:7}},["경지","전투력","나이","재능"].map(function(t){return e("button",{key:t,className:"xb"+(rType===t?" a":""),style:{flex:1,padding:"4px 0",textAlign:"center"},onClick:function(){setRType(t);setRPage(0);}},t);})),
      e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:5}},
        e("span",{style:{fontSize:10,color:"#8a6840"}},rType+"순"),
        e("div",{style:{display:"flex",gap:3}},
          e("button",{className:"xb",style:{padding:"2px 7px",fontSize:10},onClick:function(){setRPage(Math.max(0,sp-1));},disabled:sp===0},"<"),
          e("span",{style:{fontSize:10,padding:"2px 4px",color:"#8a6840"}},(sp+1)+"/"+tp),
          e("button",{className:"xb",style:{padding:"2px 7px",fontSize:10},onClick:function(){setRPage(Math.min(tp-1,sp+1));},disabled:sp>=tp-1},">"))),
      ranked.slice(sp*PS,(sp+1)*PS).map(function(c,pi){return AgCard(c,sp*PS+pi);}));
  }
  function renderAchievement(){
    return e("div",{style:{padding:"0 10px 10px"}},
      e("div",{style:{display:"flex",gap:3,paddingTop:8,marginBottom:8}},["역대최고","5개합","최종순위"].map(function(t){return e("button",{key:t,className:"xb"+(aTab===t?" a":""),style:{flex:1,padding:"4px 0",textAlign:"center"},onClick:function(){setATab(t);}},t);})),
      aTab==="역대최고"?PgCtrl(achR.byBest,"역대최고",false):
      aTab==="5개합"?PgCtrl(achR.byTop5,"5개합",true):
      achR.combined.length===0?e("div",{style:{color:"#8a6840",textAlign:"center",padding:"30px 0"}},"윤회 이력이 쌓이면 산출됩니다."):
      e("div",null,[[0,100],[100,300],[300,500],[500,700],[700,1000]].map(function(range,ti){
        var tier=TC[ti],items=achR.combined.slice(range[0],range[1]);if(!items.length)return null;
        var exp=ftExp[ti]!==false,shown=exp?items:items.slice(0,5);
        var tiCopy=ti;
        return e("div",{key:ti,style:{marginBottom:10}},
          e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"center",background:tier.co+"18",border:"1px solid "+tier.co+"40",borderRadius:8,padding:"7px 10px",marginBottom:5,cursor:"pointer"},onClick:function(){setFtExp(function(x){var n=Object.assign({},x);n[tiCopy]=!exp;return n;});}},
            e("span",{style:{fontWeight:700,color:tier.co,fontSize:12}},tier.lb),
            e("span",{style:{fontSize:10,color:"#8a6840",marginLeft:6}},"윤회점수 "+fS(tier.sc)+" / "+items.length+"명"),
            e("span",{style:{color:"#8a6840",marginLeft:"auto"}},exp?"A":"V")),
          shown.map(function(d,i){
            var rank=range[0]+i+1,ag2=W.ag.find(function(a){return a.id===d.id;}),rc2=RC[d.bHr]||"#888",rTi=GT(ag2?ag2.rcR:500);
            var dId2=d.id;
            return e("div",{key:d.id,className:"xc",style:{padding:"7px 9px",marginBottom:4,cursor:"pointer"},onClick:function(){if(ag2)setSel(ag2.id);}},
              e("div",{style:{display:"flex",alignItems:"center",gap:5}},
                e("span",{style:{width:22,height:22,borderRadius:"50%",fontSize:10,fontWeight:700,display:"inline-flex",alignItems:"center",justifyContent:"center",background:RBG(rank),color:RTX(rank),flexShrink:0}},rank),
                e("div",{style:{flex:1}},
                  e("div",{style:{fontWeight:700,fontSize:11}},d.nm+(ag2?" *":"")),
                  e("div",{style:{display:"flex",gap:3,flexWrap:"wrap",marginTop:2}},
                    e(Bdg,{v:RL[d.bHr].n,bg:rc2+"18",co:rc2,br:rc2+"40"}),
                    e(Bdg,{v:"r1:"+d.r1+" r2:"+d.r2,bg:"rgba(30,14,0,.07)",co:"#8a6840"}),
                    e(Bdg,{v:"윤회점수 "+fS(ag2?ag2.rcS:100),bg:rTi.co+"18",co:rTi.co,br:rTi.co+"40"}))),
                e("div",{style:{textAlign:"right",flexShrink:0}},
                  e("div",{style:{fontSize:11,fontWeight:700,color:"#7a4800"}},fS(d.bS)),
                  e("div",{style:{fontSize:9,color:"#8a6840"}},"5합 "+fS(d.t5)))));
          }),
          !exp&&items.length>5?e("button",{className:"xb",style:{width:"100%",marginTop:3,fontSize:10},onClick:function(){setFtExp(function(x){var n=Object.assign({},x);n[tiCopy]=true;return n;});}},"더 보기"):null);
      })));
  }
  function renderStat(){
    return e("div",{style:{padding:"0 10px 10px"}},
      e("div",{style:{marginBottom:10,paddingTop:8}},
        e("div",{style:{fontSize:12,fontWeight:700,color:"#7a4800",marginBottom:8}},"경지별 수선자 현황"),
        RL.map(function(_,ri2){
          var stages=MS.map(function(_,mi2){return W.ag.filter(function(a){return a.ri===ri2&&a.mi===mi2;}).length;});
          var total=stages.reduce(function(s,v){return s+v;},0);if(!total)return null;var co=RC[ri2]||"#7a4800";
          return e("div",{key:ri2,className:"xp",style:{padding:"9px 12px",marginBottom:6,borderLeft:"3px solid "+co}},
            e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:4}},
              e("div",{style:{fontWeight:700,fontSize:11,color:co}},RL[ri2].n+(ri2>=4?" (불사)":"")),
              e("span",{style:{fontWeight:700,fontSize:13,color:co}},total+"명")),
            e("div",{style:{display:"grid",gridTemplateColumns:"repeat(4,1fr)",gap:3}},
              MS.map(function(ms2,mi2){return e("div",{key:mi2,style:{background:"rgba(30,14,0,.05)",borderRadius:5,padding:"4px 6px",textAlign:"center",opacity:stages[mi2]>0?1:0.4}},e("div",{style:{fontSize:9,color:"#8a6840"}},ms2),e("div",{style:{fontWeight:700,fontSize:12,color:stages[mi2]>0?co:"#aaa"}},stages[mi2]),e(B,{p:total>0?(stages[mi2]/total)*100:0,c:co,h:3}));})));
        })),
      e("div",{className:"xp",style:{padding:"10px 12px",marginBottom:8}},
        e("div",{style:{fontWeight:700,color:"#7a4800",marginBottom:6}},"이념별 비교"),
        W.sc.map(function(sect){
          var mb=W.ag.filter(function(a){return a.si===sect.id&&a.al;}),ar=mb.length?mb.reduce(function(s,a){return s+a.ri;},0)/mb.length:0;
          var idio=IDEO[sect.ideo],ico=idio?idio.co:"#7a4800";
          return e("div",{key:sect.id,style:{marginBottom:8,padding:"8px 10px",background:ico+"08",borderRadius:8,borderLeft:"3px solid "+ico}},
            e("div",{style:{fontWeight:700,color:ico,fontSize:12,marginBottom:4}},sect.nm+" ("+sect.ideo+")"),
            e(KV,{lb:"구성원",vl:mb.length+"명"}),e(KV,{lb:"등록 직위",vl:smc(sect)+"명"}),e(KV,{lb:"평균 경지",vl:RL[Math.min(8,Math.floor(ar))].n}),e(KV,{lb:"누적 공헌",vl:fS(sect.contrib||0)}));
        })),
      e("div",{className:"xp",style:{padding:"10px 12px"}},
        e("div",{style:{fontWeight:700,color:"#7a4800",marginBottom:6}},"이벤트 통계"),
        Object.entries(W.lg.reduce(function(acc,l){if(l.tp)acc[l.tp]=(acc[l.tp]||0)+1;return acc;},{})).map(function(kv){return e(KV,{key:kv[0],lb:kv[0],vl:kv[1]+"건",co:LC[kv[0]]});})));
  }
  function renderLog(){
    var flog=lf==="전체"?W.lg:W.lg.filter(function(l){return l.tp===lf;});
    return e("div",{style:{padding:"0 10px 10px"}},
      e("div",{style:{display:"flex",gap:2,flexWrap:"wrap",marginBottom:8}},
        ["전체","돌파","천겁","전투","비무","대비무","부상","종문","관계","거래","전쟁","윤회"].map(function(f){
          var isA=lf===f,co=LC[f]||"#7a4800";
          return e("button",{key:f,className:"xb"+(isA?" a":""),style:isA?{padding:"3px 6px",fontSize:9,background:co,color:"#fdf0d8",borderColor:co}:{padding:"3px 6px",fontSize:9},onClick:function(){setLf(f);}},f);
        })),
      flog.slice(0,120).map(function(log,i){
        var co=LC[log.tp]||"#7a4800";
        return e("div",{key:log.tick+"-"+i,style:{padding:"6px 9px",borderRadius:7,marginBottom:4,fontSize:10,lineHeight:1.5,border:"1px solid "+co+"30",borderLeft:"3px solid "+co,background:"rgba(30,14,0,.04)"}},
          e("div",{style:{display:"flex",justifyContent:"space-between",marginBottom:2}},e("span",{style:{fontSize:8,fontWeight:700,padding:"1px 5px",borderRadius:999,background:co+"20",color:co}},log.tp),e("span",{style:{fontSize:9,color:"#8a6840"}},log.tick+"년")),
          log.tx);
      }));
  }
  function renderContent(){
    if(tab==="전쟁")return renderWar();if(tab==="종문")return renderSect();if(tab==="랭킹")return renderRanking();
    if(tab==="업적")return renderAchievement();if(tab==="현황")return renderStat();if(tab==="로그")return renderLog();return null;
  }
  var ph=wPh(tick),totalDeath=Object.values(W.ds).reduce(function(s,v){return s+v;},0);
  return e("div",{className:"x"},
    e("style",null,CSS),
    selC?e(Modal,{c:selC,sc:W.sc,ag:W.ag,onClose:function(){setSel(null);},onAgent:function(id){setSel(id);}}):null,
    selSect?e(SectModal,{sect:selSect,ag:W.ag,sc:W.sc,rg:W.rg,onClose:function(){setSectSel(null);},onAgent:function(id){setSectSel(null);setSel(id);}}):null,
    e("div",{style:{background:"#e8d8b8",borderBottom:"1px solid rgba(100,65,20,.22)",padding:"10px 12px",position:"sticky",top:0,zIndex:100}},
      e("div",{style:{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:8}},
        e("div",null,
          e("div",{style:{fontSize:14,fontWeight:700,color:"#7a4800",letterSpacing:2}},"선협 관찰"),
          e("div",{style:{fontSize:9,color:ph.war?"#c60":"#2a7",fontWeight:700}},tick.toLocaleString()+"년  "+(ph.war?"전쟁 중 ("+fN(ph.rem)+"년 남음)":"대비무 기간 ("+fN(ph.rem)+"년 남음)"))),
        e("div",{style:{display:"flex",gap:4,alignItems:"center"}},
          e("button",{className:"xb a",style:{padding:"5px 8px",fontSize:10},onClick:function(){setRun(function(v){return !v;});}},run?"정지":"재개"),
          e("button",{className:"xb",style:{padding:"5px 8px",fontSize:10},onClick:rst},"초기화"),
          e("div",{style:{display:"flex",gap:2}},[1,3,10,30,100,500].map(function(v){return e("button",{key:v,className:"xb"+(batch===v?" a":""),style:{padding:"3px 5px",fontSize:9},onClick:function(){setBatch(v);}},v+"x");})))),
      e("div",{style:{display:"grid",gridTemplateColumns:"repeat(4,1fr)",gap:4}},
        [{l:"전투",v:W.tb||0,c:"#b82"},{l:"대비무",v:W.bimu||0,c:"#3a8"},{l:"부상",v:injN,c:"#b40"},{l:"사망",v:totalDeath,c:"#666"}].map(function(s){
          return e("div",{key:s.l,style:{background:"rgba(122,72,0,.1)",border:"1px solid rgba(100,65,20,.15)",borderRadius:7,padding:"4px 7px",textAlign:"center"}},
            e("div",{style:{fontSize:8,color:"#8a6840"}},s.l),
            e("div",{style:{fontSize:12,fontWeight:700,color:s.c}},s.v.toLocaleString()));
        })),
      ldr?e("div",{style:{background:"rgba(122,72,0,.1)",borderRadius:8,padding:"6px 10px",display:"flex",alignItems:"center",gap:8,cursor:"pointer",marginTop:7},onClick:function(){setSel(ldr.id);}},
        e("div",{style:{flex:1}},
          e("div",{style:{fontWeight:700,fontSize:11,color:"#7a4800"}},ldr.nm+(ldr.ri>=4?" (불사)":"")+" / "+(ldr.sr||"무직위")),
          e("div",{style:{fontSize:9,color:"#8a6840"}},rn(ldr)+" / "+CVN[ldr.cv]+" / "+fN(cp(ldr,W.sc))+" / 공헌 "+fS(ldr.ctrib||0))),
        e("span",{style:{color:"#8a6840",fontSize:12}},">")):null),
    e("div",{style:{paddingTop:4}},renderContent()),
    e("nav",{className:"xnav"},
      [{id:"전쟁",ic:"vs"},{id:"종문",ic:"門"},{id:"랭킹",ic:"top"},{id:"업적",ic:"str"},{id:"현황",ic:"sta"},{id:"로그",ic:"log"}].map(function(nt){
        return e("button",{key:nt.id,className:"xnt"+(tab===nt.id?" a":""),onClick:function(){setTab(nt.id);}},
          e("span",{style:{fontSize:11}},nt.ic),nt.id);
      })));
}
