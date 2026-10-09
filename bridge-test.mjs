import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
class Track { constructor(){this.value=true;this.kind='audio';} get enabled(){return this.value;} set enabled(v){this.value=v;} addEventListener(){} }
const micTrack=new Track(),outTrack=new Track(),remoteTrack=new Track();
const mic={getAudioTracks:()=>[micTrack]},processed={getAudioTracks:()=>[outTrack]};
class Context { createMediaStreamDestination(){return {stream:processed};} } class Peer { addTrack(track){return track;} }
let callback,role=false;
const sandbox={MediaStreamTrack:Track,AudioContext:Context,RTCPeerConnection:Peer,navigator:{mediaDevices:{getUserMedia:async()=>mic}},document:{body:null},window:{electron:{ipcRenderer:{invoke:async()=>role}}},setInterval:fn=>callback=fn,Map};
vm.runInNewContext(fs.readFileSync(new URL('./Assets/renderer-bridge.js',import.meta.url),'utf8'),sandbox);
await sandbox.navigator.mediaDevices.getUserMedia();new sandbox.RTCPeerConnection().addTrack(outTrack);
role=true;await callback();assert.equal(micTrack.enabled,false);assert.equal(outTrack.enabled,false);assert.equal(remoteTrack.enabled,true);
// Reconnecting/adding another peer must not save the forced role mute as user intent.
await sandbox.navigator.mediaDevices.getUserMedia();
new sandbox.RTCPeerConnection().addTrack(micTrack);
new sandbox.RTCPeerConnection().addTrack(outTrack);
role=false;await callback();assert.equal(micTrack.enabled,true,'re-registered microphone must recover after silence');assert.equal(outTrack.enabled,true,'re-registered outgoing stream must recover after silence');
role=true;await callback();
micTrack.enabled=true;outTrack.enabled=true;assert.equal(micTrack.enabled,false);assert.equal(outTrack.enabled,false);
role=false;await callback();assert.equal(micTrack.enabled,true);assert.equal(outTrack.enabled,true);
micTrack.enabled=false;role=true;await callback();role=false;await callback();assert.equal(micTrack.enabled,false);
sandbox.window.electron.ipcRenderer.invoke=async()=>{throw Error('Game unavailable')};micTrack.enabled=true;role=true;await callback();assert.equal(micTrack.enabled,true);
console.log('PASS: outgoing mic and processed stream muted; remote audio untouched; role cannot be manually unmuted; own mute preserved; unavailable bridge releases role effect.');

