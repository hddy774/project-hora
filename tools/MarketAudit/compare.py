import csv,json
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from statistics import median
out=Path('docs/analysis/v1.3.0-implementation')
new=list(csv.DictReader(open(out/'monthly.csv')))
old=list(csv.DictReader(open('docs/analysis/v1.3.0/monthly.csv')))
fig,axs=plt.subplots(2,2,figsize=(11,7.5),layout='constrained')
fig.suptitle('Project Hora: 36-season market comparison',fontweight='bold')
for data,label,color in [(old,'v1.2.0','#e76f51'),(new,'v1.3.0','#218c74')]:
 group=[r for r in data if r['scenario']=='baseline']
 for ax,column,title in [(axs[0,0],'capitalization_return','Market capitalization (initial = 100)'),(axs[0,1],'investor_cash_change','Investor cash (initial = 100)')]:
  values=[[100*(1+float(r[column])) for r in group if int(r['season'])==s] for s in range(37)]
  ax.plot(range(37),[median(v) for v in values],label=label,color=color,lw=2)
  ax.fill_between(range(37),[min(v) for v in values],[max(v) for v in values],alpha=.14,color=color)
  ax.set_title(title);ax.set_xlabel('Season');ax.grid(alpha=.18);ax.legend()
for col,title in [('price_index','Price index'),('total_return_index','Total-return index')]:
 vals=[[float(r[col]) for r in new if r['scenario']=='baseline' and int(r['season'])==s] for s in range(37)]
 axs[1,0].plot(range(37),[median(v) for v in vals],label=title,lw=2)
axs[1,0].set_title('v1.3.0: price / dividend total-return indices');axs[1,0].set_xlabel('Season');axs[1,0].legend();axs[1,0].grid(alpha=.18)
rows=[r for r in new if r['scenario']=='baseline' and int(r['season'])==36]
axs[1,1].bar(range(len(rows)),[float(r['cumulative_declared_dividends'])/1e6 for r in rows],color='#218c74',label='Company dividends paid')
axs[1,1].bar(range(len(rows)),[float(r['paid_dividends'])/1e6 for r in rows],color='#67bdaf',label='Investor gross receipts')
axs[1,1].set_xticks(range(len(rows)),[r['seed'] for r in rows],rotation=20);axs[1,1].set_title('Actual paid dividends (million KRW)');axs[1,1].legend();axs[1,1].grid(axis='y',alpha=.18)
fig.savefig(out/'market-comparison.svg')
x=json.load(open(out/'summary.json'))
for s in x['summaries']:
 print(s['scenario'],s['seed'],f"cap {s['capitalizationReturn']:.4%}; cash {s['investorCashChange']:.4%}; equity {s['investorEquityReturn']:.4%}; price {s['priceIndex']:.2f}; total {s['totalReturnIndex']:.2f}; dividends {s['reportedDividends']}; paid {s['paidDividends']}; down {s['decliningSeasons']}")
