using System.Text.Json.Nodes;
namespace Forger;
internal static class UpdateNotice
{
    internal static string Consume(string path,string installedVersion)
    {
        if(!File.Exists(path))return "";
        var status=JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        if(status==null)return "";
        var message=status["message"]?.GetValue<string>()??"";
        if(!message.StartsWith("Updated to ",StringComparison.Ordinal))return message;
        if(status["noticeSeen"]?.GetValue<bool>()==true)return "";
        status["noticeSeen"]=true;
        var pending=path+".notice-new";
        File.WriteAllText(pending,status.ToJsonString());File.Move(pending,path,true);
        // An older result can survive a manual install. Never show it as the running version.
        return message.StartsWith("Updated to "+installedVersion+".",StringComparison.Ordinal)
            ?"Updated to v"+installedVersion+".":"";
    }
}
