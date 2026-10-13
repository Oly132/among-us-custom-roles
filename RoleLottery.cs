namespace Forger;
internal static class RoleLottery
{
    internal static int[] Select(int[] counts,int[] chances,int slots,Random random)
    {
        var guaranteed=new List<int>();var eligible=new List<int>();
        for(int i=0;i<counts.Length;i++)
        {
            if(counts[i]<=0)continue;
            if(chances[i]>=100)guaranteed.Add(i);
            else if(random.Next(100)<chances[i])eligible.Add(i);
        }
        void Shuffle(List<int> list){for(int i=list.Count-1;i>0;i--){int j=random.Next(i+1);(list[i],list[j])=(list[j],list[i]);}}
        Shuffle(guaranteed);Shuffle(eligible);
        return guaranteed.Concat(eligible).Take(Math.Max(0,slots)).ToArray();
    }
}
