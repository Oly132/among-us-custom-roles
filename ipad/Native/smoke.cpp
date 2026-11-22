#include "forger_rules.h"
#include <cstdio>
#include <cstdlib>
#include <limits>
static int checks=0;
static void check(bool result,const char* message) {
    ++checks;if(!result){std::fprintf(stderr,"FAIL: %s\n",message);std::exit(1);}
}
int main() {
    check(forger_rules_abi()==1,"ABI version");
    forger_rules_config c={};c.size=sizeof(c);c.abi=1;
    c.forger=1;c.silencer=2;c.thief=3;c.fat_guy=4;c.lockpicker=5;
    c.forge_uses=3;c.forge_cooldown=20;c.forge_window=10;
    c.door_cooldown_enabled=1;c.doors_per_use=2;c.door_cooldown=15;
    check(forger_rules_create(nullptr,1)==0,"Null configuration");
    const auto h=forger_rules_create(&c,123);check(h!=0,"Create context");
    check(forger_rules_event(h,FORGER_KILLED,1,6,0,1)==1,"Create evidence");
    check(forger_rules_event(h,FORGE,1,6,7,2)==1,"Change evidence");
    forger_rules_snapshot_data state={};state.size=sizeof(state);
    check(forger_rules_snapshot(h,1,2,&state)==1 && state.forge_uses_remaining==2 && state.forge_ready_at==22,"Evidence snapshot");
    check(forger_rules_event(h,APPLY_SILENCE,2,6,0,3)==1,"Apply silence");
    check(forger_rules_event(h,MEETING_STARTED,0,0,0,4)==1,"Meeting start");
    check(forger_rules_event(h,MEETING_ENDED,0,0,0,5)==1,"Meeting end");
    state.size=sizeof(state);
    check(forger_rules_snapshot(h,6,5,&state)==1 && state.silence_seconds_remaining==10,"Countdown starts at ten");
    check(forger_rules_event(h,RULES_TICK,0,0,0,15)==1,"Expire silence");
    state.size=sizeof(state);
    check(forger_rules_snapshot(h,6,15,&state)==1 && state.silenced==0,"Silence expired");
    check(forger_rules_event(h,APPLY_SILENCE,2,6,0,16)==0,"No repeat silence");
    check(forger_rules_event(h,ORDINARY_STAB,8,4,0,17)==1,"First stab protected");
    check(forger_rules_event(h,ORDINARY_STAB,8,4,0,18)==0,"Second stab unprotected");
    check(forger_rules_event(h,OPEN_SABOTAGED_DOOR,5,0,0,19)==1,"First lockpick");
    check(forger_rules_event(h,OPEN_SABOTAGED_DOOR,5,0,0,20)==1,"Second lockpick");
    check(forger_rules_event(h,OPEN_SABOTAGED_DOOR,5,0,0,21)==0,"Lockpick cooldown");
    check(forger_rules_event(h,TRY_STEAL,3,6,0,22)==1,"First steal");
    check(forger_rules_event(h,TRY_STEAL,3,7,0,23)==0,"No second steal");
    check(forger_rules_event(h,RULES_TICK,0,0,0,std::numeric_limits<double>::quiet_NaN())==-1,"Reject invalid clock");
    check(forger_rules_destroy(h)==1,"Destroy context");
    check(forger_rules_destroy(h)==-2,"Reject stale handle");
    std::printf("PASS: %d native C++/NativeAOT checks on build host. Not an iPad game test.\n",checks);
}
