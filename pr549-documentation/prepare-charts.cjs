const fs=require('fs'), path=require('path');
const dir=__dirname, out=path.join(dir,'assets'); fs.mkdirSync(out,{recursive:true});
const report=fs.readFileSync(path.join(dir,'perf/report.md'),'utf8');
const rows=report.split('\n').filter(l=>/^\| `/.test(l)).map(l=>{ const c=l.split('|').slice(1,-1).map(s=>s.trim()); return {id:c[0].replaceAll('`',''),label:c[1],base:c[2],head:c[3],delta:parseFloat(c[4]),wallBase:parseFloat(c[5].replaceAll(',','')),wallHead:parseFloat(c[6].replaceAll(',','')),verdict:c[7]}; });
fs.writeFileSync(path.join(out,'engine-comparison.csv'),'scenario,label,base_work,pr_work,work_change_pct,base_wall_ms,pr_wall_ms,verdict\n'+rows.map(r=>[r.id,'"'+r.label.replaceAll('"','""')+'"','"'+r.base+'"','"'+r.head+'"',r.delta,r.wallBase,r.wallHead,r.verdict].join(',')).join('\n')+'\n');
for(const name of ['report.md','results-base.tsv','results-head.tsv']) fs.copyFileSync(path.join(dir,'perf',name),path.join(out,name));
const esc=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;');
function chart(title,subtitle,selected,metric,file,lo,hi,unit,limit) {
 const w=1050,h=150+selected.length*42,left=290,right=900,x=n=>left+(n-lo)/(hi-lo)*(right-left), bottom=100+selected.length*42;
 let s=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"><rect width="100%" height="100%" fill="#ffffff"/><g font-family="Arial, sans-serif" fill="#19222c"><text x="24" y="30" font-size="22" font-weight="bold">${title}</text><text x="24" y="58" font-size="13">${subtitle}</text>`;
 for(const tick of [lo,0,hi]) s+=`<path d="M ${x(tick)} 84 V ${bottom}" stroke="#cfd6de"/><text x="${x(tick)}" y="${bottom+27}" text-anchor="middle" font-size="12">${tick}${unit}</text>`;
 if(limit!==undefined) s+=`<path d="M ${x(limit)} 76 V ${bottom}" stroke="#bf3d22" stroke-dasharray="5 4"/><text x="${x(limit)}" y="${bottom+44}" text-anchor="middle" font-size="12" fill="#bf3d22">gate +${limit}${unit}</text>`;
 selected.forEach((r,i)=>{const n=metric(r),y=95+i*42; s+=`<text x="${left-16}" y="${y+6}" text-anchor="end" font-size="14">${esc(r.id)}</text><rect x="${Math.min(x(0),x(n))}" y="${y-10}" width="${Math.max(1,Math.abs(x(n)-x(0)))}" height="22" fill="${n>0?'#b45c28':'#287997'}"/><text x="${right+20}" y="${y+6}" font-size="14">${n>=0?'+':''}${n.toFixed(2)}${unit}</text>`; });
 fs.writeFileSync(path.join(out,file+'.svg'),s+'</g></svg>');
}
chart('Engine instruction work: base vs PR #549','Linux / Valgrind · largest absolute changes · every scenario passed',rows.slice().sort((a,b)=>Math.abs(b.delta)-Math.abs(a.delta)).slice(0,12),r=>r.delta,'engine-work',-.8,1.1,'%',1);
const chosen=['mix-1','mix-8','mix-16','mix-32','silent-32','sparse-32','aurora-supersaw-2x','instrument-00'];
chart('Offline render time: base vs PR #549','Fastest of 5 interleaved timing rounds on the same CI runner · lower is better',chosen.map(id=>rows.find(r=>r.id===id)),r=>(r.wallHead/r.wallBase-1)*100,'render-time',-2,16,'%',15);
fs.copyFileSync(path.join(dir,'../pr549-mute-perf/edit-warp-split.csv'),path.join(out,'windows-edit-warp-split.csv'));
fs.copyFileSync(path.join(dir,'../pr549-mute-perf/edit-warp-split.svg'),path.join(out,'windows-edit-warp-split.svg'));
const sharp=require(path.join(dir,'docs-archive/nota-daw-nota-docs-05732b0/node_modules/sharp'));
(async()=>{for(const name of ['engine-work','render-time','windows-edit-warp-split'])await sharp(path.join(out,name+'.svg'),{density:144}).png().toFile(path.join(out,name+'.png')); console.log('Prepared charts for '+rows.length+' scenarios.');})();
