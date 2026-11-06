using System.Security.Cryptography;
namespace Forger.Radio;

// Public receiver protocol. No publishing key or capture implementation belongs here.
public static class RadioWire
{
    public const byte AudioRpc=220, ClockRequestRpc=221, ClockReplyRpc=222;
    public const int Rate=24000, Samples=960, BlockBytes=484, SignatureBytes=64, MaxPacket=700;
    public const double BufferSeconds=.85;
    public const string PublicKey="MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEzKDe7E+9lZcxwpQxR974+qebGcElX4zvGXiS4iY7vBlqLC6/ZoOexhc0+TudY2ffjRXjFApCkHl4BUi65A6d8g==";
    public sealed class Frame
    {
        public byte Kind,Player;
        public int Lobby,Sequence;
        public Guid Stream;
        public double Time;
        public byte[] Data=Array.Empty<byte>();
    }
    public static byte[] Payload(Frame f)
    {
        using var s=new MemoryStream();using var w=new BinaryWriter(s);
        w.Write((byte)1);w.Write(f.Kind);w.Write(f.Lobby);w.Write(f.Player);w.Write(f.Stream.ToByteArray());
        w.Write(f.Sequence);w.Write(f.Time);w.Write((ushort)f.Data.Length);w.Write(f.Data);
        return s.ToArray();
    }
    public static Frame? Verify(byte[] packet,ECDsa key,int lobby,byte sender)
    {
        if(packet.Length<101 || packet.Length>MaxPacket)return null;
        int size=packet.Length-SignatureBytes;
        if(!key.VerifyData(packet.AsSpan(0,size),packet.AsSpan(size),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))return null;
        using var r=new BinaryReader(new MemoryStream(packet,0,size));
        if(r.ReadByte()!=1)return null;
        var f=new Frame{Kind=r.ReadByte(),Lobby=r.ReadInt32(),Player=r.ReadByte(),Stream=new Guid(r.ReadBytes(16)),Sequence=r.ReadInt32(),Time=r.ReadDouble()};
        int count=r.ReadUInt16();
        if(f.Lobby!=lobby || f.Player!=sender || !double.IsFinite(f.Time) || f.Time<0 || count!=size-r.BaseStream.Position || f.Kind>3 || f.Sequence<0)return null;
        if(f.Kind==1 && count!=BlockBytes)return null;
        if((f.Kind==0 || f.Kind==2) && count!=0)return null;
        if(f.Kind==3 && count!=16)return null;
        f.Data=r.ReadBytes(count);return f;
    }
    public static ECDsa ReceiverKey()
    {
        var key=ECDsa.Create();key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKey),out _);return key;
    }
}

// Independent IMA ADPCM blocks: loss of one packet cannot corrupt later audio.
public static class RadioAdpcm
{
    internal static readonly int[] Steps={7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767};
    internal static readonly int[] Changes={-1,-1,-1,-1,2,4,6,8};
    public static float[] Decode(byte[] data)
    {
        if(data.Length!=RadioWire.BlockBytes || data[2]>88 || data[3]!=0)throw new ArgumentException("Invalid audio block");
        int value=(short)(data[0]|data[1]<<8),index=data[2];var samples=new float[RadioWire.Samples];
        for(int i=0;i<samples.Length;i++)
        {
            int code=(data[4+i/2]>>((i&1)*4))&15,step=Steps[index],delta=(step>>3)+((code&4)!=0?step:0)+((code&2)!=0?step>>1:0)+((code&1)!=0?step>>2:0);
            value=Math.Clamp(value+((code&8)!=0?-delta:delta),short.MinValue,short.MaxValue);index=Math.Clamp(index+Changes[code&7],0,88);
            samples[i]=value/32768f;
        }
        return samples;
    }
}
