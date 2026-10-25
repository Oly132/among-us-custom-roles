using System.Text.Json.Nodes;
namespace Forger;

// Preserve private addresses while selecting a genuine official matchmaker.
internal static class VanillaRegionPolicy
{
    internal static string SelectOfficial(string json,string preferred="Europe")
    {
        var root=JsonNode.Parse(json.TrimStart('\uFEFF'))?.AsObject()??throw new InvalidDataException("Invalid region settings");
        var regions=root["Regions"]?.AsArray()??throw new InvalidDataException("Missing region list");
        var official=new[]{("Europe","matchmaker-eu.among.us",290),("North America","matchmaker.among.us",289),("Asia","matchmaker-as.among.us",291)};
        var choices=official.OrderByDescending(x=>x.Item1==preferred).ToArray();
        foreach(var (name,host,translation) in choices)
        {
            for(var i=0;i<regions.Count;i++)
            {
                var region=regions[i];if(region?["Name"]?.GetValue<string>()!=name)continue;
                var servers=region?["Servers"]?.AsArray();
                if(servers==null||servers.Count==0||servers.Any(server=>!IsOfficial(server?["Ip"]?.GetValue<string>(),host)))continue;
                root["CurrentRegionIdx"]=i;return root.ToJsonString();
            }
        }
        var target=choices[0];
        regions.Add(new JsonObject{["$type"]="StaticHttpRegionInfo, Assembly-CSharp",["Name"]=target.Item1,["PingServer"]=target.Item2,["TranslateName"]=target.Item3,["TargetServer"]=null,
            ["Servers"]=new JsonArray(new JsonObject{["Name"]="Http-1",["Ip"]="https://"+target.Item2,["Port"]=443,["UseDtls"]=false,["Players"]=0,["ConnectionFailures"]=0})});
        root["CurrentRegionIdx"]=regions.Count-1;return root.ToJsonString();
    }
    static bool IsOfficial(string? address,string host)=>Uri.TryCreate(address,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host==host&&uri.Port==443&&uri.UserInfo.Length==0&&uri.AbsolutePath=="/"&&uri.Query.Length==0&&uri.Fragment.Length==0;
}
