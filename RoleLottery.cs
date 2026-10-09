namespace Forger;
internal static class RoleLottery
{
    internal static int[] Select(int[] counts,int[] chances,int slots,Random random)
    {
        var eligible=new List<int>();
        for(int i=0;i<counts.Length;i++)if(counts[i]>0 && random.Next(100)<chances[i])eligible.Add(i);
        for(int i=eligible.Count-1;i>0;i--){int j=random.Next(i+1);(eligible[i],eligible[j])=(eligible[j],eligible[i]);}
        return eligible.Take(Math.Max(0,slots)).ToArray();
    }
}
