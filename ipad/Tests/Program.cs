using Forger.IPad;
using System.Runtime.InteropServices;
unsafe class Program
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        delegate* unmanaged[Cdecl]<uint> abi=&NativeRules.Abi;
        delegate* unmanaged[Cdecl]<RulesConfig*,uint,uint> create=&NativeRules.Create;
        delegate* unmanaged[Cdecl]<uint,int> destroy=&NativeRules.Destroy;
        delegate* unmanaged[Cdecl]<uint,int,byte,byte,byte,double,int> apply=&NativeRules.Event;
        delegate* unmanaged[Cdecl]<uint,byte,double,RulesSnapshot*,int> read=&NativeRules.Snapshot;
        Check(abi()==1,"ABI version");Check(sizeof(RulesConfig)==36 && sizeof(RulesSnapshot)==48,"Native struct sizes");
        Check(Marshal.OffsetOf<RulesSnapshot>(nameof(RulesSnapshot.EvidenceDeadline)).ToInt32()==24,"Native double alignment");
        var config=new RulesConfig{Size=36,Abi=1,Forger=1,Silencer=2,Thief=3,FatGuy=4,Lockpicker=5,ForgeUses=3,ForgeCooldown=20,ForgeWindow=10,DoorsPerUse=2,DoorCooldown=15,DoorCooldownEnabled=1};
        Check(create(null,1)==0,"Null config");var bad=config;bad.Abi=2;Check(create(&bad,1)==0,"Unknown ABI");
        bad=config;bad.FatGuy=bad.Forger;Check(create(&bad,1)==0,"Duplicate role ownership");
        uint h=create(&config,99);Check(h!=0,"Create real shared rules");
        int Event(RulesEvent kind,byte actor,byte target,byte value,double time)=>apply(h,(int)kind,actor,target,value,time);
        RulesSnapshot Snapshot(byte player,double time)
        {
            var data=new RulesSnapshot{Size=48};Check(read(h,player,time,&data)==1,"Read snapshot");return data;
        }
        Check(Event(RulesEvent.ForgerKilled,1,6,0,1)==1,"Forger kill evidence");
        Check(Event(RulesEvent.Forge,1,6,7,2)==1,"Forger changes evidence");
        var snapshot=Snapshot(1,2);Check(snapshot.ForgeUsesRemaining==2 && snapshot.ForgeReadyAt==22,"Uses/cooldown preserved");
        Check(Event(RulesEvent.ForgerKilled,1,7,0,3)==1,"Next victim");
        Check(Event(RulesEvent.Forge,1,7,8,4)==0,"Cooldown still blocks forging");
        Check(Event(RulesEvent.ApplySilence,2,6,0,5)==1,"Silence applied");
        Check(Snapshot(6,5).SilenceSecondsRemaining==-1,"Muted until meeting");
        Check(Event(RulesEvent.MeetingStarted,0,0,0,6)==1,"Meeting starts");
        Check(Event(RulesEvent.MeetingEnded,0,0,0,10)==1,"Meeting ends");
        Check(Snapshot(6,10).SilenceSecondsRemaining==10,"Ten seconds after meeting");
        Check(Snapshot(6,19).SilenceSecondsRemaining==1,"Countdown reaches one");
        Check(Event(RulesEvent.Tick,0,0,0,20)==1 && Snapshot(6,20).Silenced==0,"Unmuted exactly at ten seconds");
        Check(Event(RulesEvent.ApplySilence,2,6,0,21)==0,"Cannot silence player twice");
        Check(Event(RulesEvent.OrdinaryStab,8,4,0,22)==1,"Fat Guy first stab protected");
        Check(Event(RulesEvent.OrdinaryStab,8,4,0,23)==0,"Second stab not protected");
        Check(Event(RulesEvent.OpenSabotagedDoor,5,0,0,24)==1,"First door opens");
        Check(Event(RulesEvent.OpenSabotagedDoor,5,0,0,25)==1,"Second door opens");
        Check(Event(RulesEvent.OpenSabotagedDoor,5,0,0,26)==0,"Door cooldown applies");
        Check(Event(RulesEvent.TrySteal,3,6,0,27)==1 && Event(RulesEvent.TrySteal,3,7,0,28)==0,"Thief once per game");
        Check(Event(RulesEvent.Tick,0,0,0,double.NaN)==NativeRules.InvalidArgument,"NaN rejected across native boundary");
        Check(Event(RulesEvent.Tick,0,0,0,1)==NativeRules.InvalidArgument,"Regressing host time rejected");
        Check(read(h,1,29,null)==NativeRules.InvalidArgument,"Null output rejected");
        Check(destroy(h)==1 && destroy(h)==NativeRules.UnknownContext,"Destroy/stale handle");
        Check(apply(h,1,1,6,0,30)==NativeRules.UnknownContext,"No stale state after new round");
        uint next=create(&config,100);Check(next!=h && next!=0,"Next epoch gets a new context");destroy(next);
        Console.WriteLine($"PASS: {checks} native-call boundary/shared-rule assertions. Host: {RuntimeInformation.OSDescription}; not an iPad gameplay test.");
    }
}
