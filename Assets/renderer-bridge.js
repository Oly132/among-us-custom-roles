// Local-game effect is independent of the user's own mute/PTT setting.
(() => {
  const tracks = new Map();
  const descriptor = Object.getOwnPropertyDescriptor(MediaStreamTrack.prototype, 'enabled');
  let silenced = false;
  function register(stream) {
    for (const track of stream.getAudioTracks()) {
      // A track may be reused for multiple peers while temporarily silenced.
      // Preserve its original user intent rather than recording our forced false.
      if (tracks.has(track)) continue;
      tracks.set(track, descriptor.get.call(track));
      track.addEventListener('ended', () => tracks.delete(track), {once:true});
      descriptor.set.call(track, silenced ? false : tracks.get(track));
    }
    return stream;
  }
  Object.defineProperty(MediaStreamTrack.prototype, 'enabled', {
    ...descriptor,
    set(value) {
      if (tracks.has(this)) {
        tracks.set(this, Boolean(value));
        descriptor.set.call(this, silenced ? false : Boolean(value));
      } else descriptor.set.call(this, value);
    }
  });
  const getUserMedia = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
  navigator.mediaDevices.getUserMedia = async (...args) => register(await getUserMedia(...args));
  // Register only transmitted tracks. Speaker-output streams must stay enabled.
  const addTrack = RTCPeerConnection.prototype.addTrack;
  RTCPeerConnection.prototype.addTrack = function (track, ...streams) {
    if(track.kind==='audio') register({getAudioTracks:()=>[track]});
    return addTrack.call(this,track,...streams);
  };
  let badge;
  function update(muted) {
    silenced = muted;
    for (const [track,desired] of tracks) descriptor.set.call(track, silenced ? false : desired);
    if (!badge && document.body) {
      badge=document.createElement('div');badge.textContent='SILENCED BY ROLE · microphone blocked';
      badge.style.cssText='position:fixed;top:0;left:0;right:0;z-index:99999;background:#a80000;color:white;text-align:center;padding:6px;font:14px sans-serif;pointer-events:none';
      document.body.appendChild(badge);
    }
    if(badge) badge.style.display=silenced?'block':'none';
  }
  let busy=false;
  setInterval(async()=>{
    if(busy)return;busy=true;
    try { update((await window.electron.ipcRenderer.invoke('forger-silence-state'))===true); }
    catch { update(false); }
    finally { busy=false; }
  },100);
})();

