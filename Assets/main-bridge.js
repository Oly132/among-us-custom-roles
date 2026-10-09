// Local-only state file. A stopped/crashed game cannot leave voice muted forever.
ipcMain.handle('forger-silence-state', () => {
  try {
    const filename=path.join(process.env.LOCALAPPDATA || app.getPath('userData'),'Forger','silencer.json');
    const state=JSON.parse(fs.readFileSync(filename,'utf8'));
    if(state.version!==1 || state.muted!==true || !Number.isInteger(state.pid) || state.pid<=0 || Date.now()-state.updated>2000 || state.updated>Date.now()+1000) return false;
    process.kill(state.pid,0);
    return true;
  } catch { return false; }
});
