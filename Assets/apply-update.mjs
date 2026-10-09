import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {Readable} from 'node:stream';
import {pipeline} from 'node:stream/promises';
export const repository='Oly132/among-us-custom-roles';
export const gameHash='615a95c9e09b6ffd45570883cb8459311c2b457533a38b37ed1704d6205aedf9';
export function digest(file){return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');}
export function validate(manifest){
 if(manifest.schema!==1||manifest.repository!==repository||!/^\d+\.\d+\.\d+$/.test(manifest.version)||manifest.gameAssemblySha256!==gameHash||!Array.isArray(manifest.files)||manifest.files.length>1000)throw Error('Unsupported release manifest');
 const seen=new Set();let total=0;
 for(const file of manifest.files){
  const p=file.path;
  if(typeof p!=='string'||!/^[a-zA-Z0-9_./-]+$/.test(p)||p.split('/').some(s=>!s||s==='.'||s==='..'))throw Error('Invalid update path');
  const allowed=file.target==='voice'?p==='resources/app.asar':file.target==='game' && (
   p==='BepInEx/plugins/Forger.dll'||/^BepInEx\/(core|unity-libs)\/.+\.(dll|json|zip)$/.test(p)||/^dotnet\/.+\.(dll|json)$/.test(p)||['winhttp.dll','doorstop_config.ini','.doorstop_version'].includes(p)||/^ForgerSetup\/(Voice|Updater)\/.+\.(js|mjs|exe|dll|json)$/.test(p));
  if(!allowed||seen.has(file.target+':'+p.toLowerCase())||!Number.isSafeInteger(file.size)||file.size<1||file.size>500*1024*1024||!/^[a-f0-9]{64}$/.test(file.sha256))throw Error('Invalid release file');
  const url=new URL(file.url);
  if(url.protocol!=='https:'||url.hostname!=='github.com'||url.username||url.password||url.port||url.search||url.hash||!url.pathname.startsWith('/'+repository+'/releases/download/')||url.pathname.includes('..'))throw Error('Untrusted release URL');
  seen.add(file.target+':'+p.toLowerCase());total+=file.size;
 }
 if(total>2*1024*1024*1024)throw Error('Release too large');
 if(manifest.serverUrl){const u=new URL(manifest.serverUrl);if(u.protocol!=='https:'||u.username||u.password||u.pathname!=='/'||u.search||u.hash)throw Error('Invalid server address');}
 return manifest;
}
export function mergeRegion(data,url){
 const u=new URL(url),regions=[...(data.Regions??[])];const index=regions.findIndex(r=>r.Name==='modded server');
 const region={'$type':'StaticHttpRegionInfo, Assembly-CSharp',Name:'modded server',PingServer:u.hostname,Servers:[{Name:'Http-1',Ip:u.origin,Port:Number(u.port||443),UseDtls:false,Players:0,ConnectionFailures:0}],TargetServer:null,TranslateName:1003};
 if(index<0)regions.push(region);else regions[index]=region;
 return {...data,Regions:regions};
}
export function destination(root,p){
 const base=fs.realpathSync(root);const result=path.resolve(base,...p.split('/'));
 if(!result.startsWith(base+path.sep))throw Error('Path escapes installation');
 let current=base;
 for(const part of p.split('/')){current=path.join(current,part);if(fs.existsSync(current)&&fs.lstatSync(current).isSymbolicLink())throw Error('Symlink update target');}
 return result;
}
export function changedFiles(manifest,roots){
 return validate(manifest).files.filter(file=>{
  if(!roots[file.target]||!fs.existsSync(roots[file.target])){if(file.target==='voice')return false;throw Error('Missing installation');}
  const dest=destination(roots[file.target],file.path);return !fs.existsSync(dest)||digest(dest)!==file.sha256;
 });
}
export function applyFiles(files,roots,cache,backup){
 const completed=[];fs.mkdirSync(backup,{recursive:true});
 try{
  for(let i=0;i<files.length;i++){
   const file=files[i],source=path.join(cache,file.sha256),dest=destination(roots[file.target],file.path),old=path.join(backup,String(i));
   if(digest(source)!==file.sha256||fs.statSync(source).size!==file.size)throw Error('Staged checksum failed');
   const exists=fs.existsSync(dest);if(exists)fs.copyFileSync(dest,old);
   fs.mkdirSync(path.dirname(dest),{recursive:true});const temporary=dest+'.forger-new';
   try{fs.copyFileSync(source,temporary);fs.renameSync(temporary,dest);completed.push({dest,old,exists});}
   finally{if(fs.existsSync(temporary))fs.unlinkSync(temporary);}
  }
 }catch(error){for(const item of completed.reverse()){if(item.exists)fs.copyFileSync(item.old,item.dest);else if(fs.existsSync(item.dest))fs.unlinkSync(item.dest);}throw error;}
}
export async function stageFiles(files,cache,fetcher=fetch,status=()=>{}){
 fs.mkdirSync(cache,{recursive:true});
 for(let i=0;i<files.length;i++){
  const file=files[i],dest=path.join(cache,file.sha256);status(`Downloading changed file ${i+1}/${files.length}`);
  if(fs.existsSync(dest)&&digest(dest)===file.sha256&&fs.statSync(dest).size===file.size)continue;
  const partial=dest+'.part';const response=await fetcher(file.url,{signal:AbortSignal.timeout(300000)});
  if(!response.ok)throw Error(`Download failed (${response.status})`);
  let count=0;await pipeline(Readable.fromWeb(response.body),async function*(source){for await(const chunk of source){count+=chunk.length;if(count>file.size)throw Error('Download larger than manifest');yield chunk;}},fs.createWriteStream(partial));
  if(count!==file.size||digest(partial)!==file.sha256)throw Error('Download checksum failed');fs.renameSync(partial,dest);
 }
}
async function main(){
 const [, , , game,pidText,plan]=process.argv;
 const root=fs.realpathSync(game),folder=path.join(root,'ForgerSetup','Updater');fs.mkdirSync(folder,{recursive:true});
 const status=message=>fs.writeFileSync(path.join(folder,'status.json'),JSON.stringify({message,updated:Date.now()}));
 try{
  const manifest=validate(JSON.parse(fs.readFileSync(plan,'utf8')));
  if(digest(path.join(root,'GameAssembly.dll'))!==gameHash)throw Error('Among Us version is unsupported; no files changed');
  const roots={game:root,voice:process.env.LOCALAPPDATA?path.join(process.env.LOCALAPPDATA,'Programs','SilencerCrewLink'):null};
  const files=changedFiles(manifest,roots),cache=path.join(folder,'cache');fs.mkdirSync(cache,{recursive:true});
  await stageFiles(files,cache,fetch,status);
  const pid=Number(pidText);if(!Number.isInteger(pid)||pid<1)throw Error('Invalid game process');
  status('Downloaded. Waiting for Among Us to close.');
  const deadline=Date.now()+20*60*1000;
  while(true){try{process.kill(pid,0);}catch{break;}if(Date.now()>deadline)throw Error('Close Among Us, then try again');await new Promise(r=>setTimeout(r,500));}
  applyFiles(files,roots,cache,path.join(folder,'backup-'+Date.now()));
  if(manifest.serverUrl&&process.env.LOCALAPPDATA){
   const region=path.join(process.env.LOCALAPPDATA,'..','LocalLow','Innersloth','Among Us','regionInfo.json');
   if(fs.existsSync(region)){const data=mergeRegion(JSON.parse(fs.readFileSync(region,'utf8')),manifest.serverUrl);fs.copyFileSync(region,region+'.Forger-update-backup');fs.writeFileSync(region,JSON.stringify(data,null,2));}
  }
  const marker=path.join(root,'ForgerSetup','installed.json');const data=fs.existsSync(marker)?JSON.parse(fs.readFileSync(marker,'utf8')):{};
  data.Version=manifest.version;fs.writeFileSync(marker,JSON.stringify(data,null,2));
  status(`Updated to ${manifest.version}. Launch Among Us through Steam.`);
 }catch(error){status('Update failed: '+error.message+'. Relaunch the game to retry.');process.exitCode=1;}
}
if(process.argv[2]==='--apply'&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))await main();
