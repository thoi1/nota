# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
import csv,datetime,json,os,pathlib,platform,random,struct,subprocess,time,wave
prep=pathlib.Path(__file__).resolve().parent;out=prep/'evidence';out.mkdir(exist_ok=True)
clip=out/'clip.wav';rng=random.Random(1)
with wave.open(str(clip),'wb') as w:
 w.setnchannels(2);w.setsampwidth(2);w.setframerate(48000);w.writeframes(b''.join(struct.pack('<hh',rng.randint(-8000,8000),rng.randint(-8000,8000)) for _ in range(48000*12)))
scenarios=[['idle'],['audio','1','offline'],['audio','8','offline'],['inst','8','offline'],['audio','8','rt'],['inst','8','rt'],['audio','32','rt', '64'],['audio','16','export']]
with (out/'results.csv').open('w',newline='') as f:
 writer=csv.writer(f);writer.writerow(['label','run','scenario','tracks','mode','block','cpu_ms_per_s','wall_ms_per_s','cpu_pct','misses']);f.flush()
 for run in range(1,4):
  for scenario in scenarios:
   for label in ['before','after']:
    args=scenario[:3]+([str(clip)]+scenario[3:] if scenario[0]!='idle' else [])
    binary=prep/'libs'/label/'engine-bench.exe'
    result=subprocess.run([str(binary)]+args,cwd=binary.parent,capture_output=True,text=True,check=True)
    row=result.stdout.strip().splitlines()[-1];writer.writerow([label,run]+next(csv.reader([row])));f.flush();print(f'run {run} {label}: {row}',flush=True)
print('BENCHMARKS COMPLETE',flush=True)
