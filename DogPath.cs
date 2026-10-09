using UnityEngine;
namespace Forger;
internal static class DogPath
{
    static bool Clear(Vector2 a,Vector2 b)=>!Physics2D.Linecast(a,b,Constants.ShipAndObjectsMask).collider;
    internal static Vector2[] Find(Vector2 start,Vector2 end)
    {
        if(Clear(start,end))return new[]{start,end};
        const float step=.5f;
        var target=(x:(int)MathF.Round((end.x-start.x)/step),y:(int)MathF.Round((end.y-start.y)/step));
        Vector2 Point((int x,int y) n)=>start+new Vector2(n.x*step,n.y*step);
        var open=new PriorityQueue<(int x,int y),float>();var cost=new Dictionary<(int,int),float>{{(0,0),0}};var previous=new Dictionary<(int,int),(int,int)>();
        open.Enqueue((0,0),0);(int x,int y)? found=null;
        var limit=Math.Max(40,Math.Max(Math.Abs(target.x),Math.Abs(target.y))+50);
        for(int visited=0;open.Count>0 && visited<12000;visited++)
        {
            var current=open.Dequeue();var position=Point(current);
            if(Vector2.Distance(position,end)<.85f && Clear(position,end)){found=current;break;}
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
            {
                if(x==0&&y==0)continue;var next=(x:current.x+x,y:current.y+y);if(Math.Abs(next.x)>limit||Math.Abs(next.y)>limit)continue;
                var nextPosition=Point(next);if(Physics2D.OverlapCircle(nextPosition,.12f,Constants.ShipAndObjectsMask) || !Clear(position,nextPosition))continue;
                var g=cost[current]+(x!=0&&y!=0?.7071f:.5f);if(cost.TryGetValue(next,out var old)&&old<=g)continue;
                cost[next]=g;previous[next]=current;open.Enqueue(next,g+Vector2.Distance(nextPosition,end));
            }
        }
        if(found==null)return new[]{start,end};
        var route=new List<Vector2>{end};var node=found.Value;route.Add(Point(node));while(previous.TryGetValue(node,out var prior)){node=prior;route.Add(Point(node));}route.Reverse();
        var simplified=new List<Vector2>{start};int at=0;
        while(at<route.Count-1){var next=at+1;while(next+1<route.Count && Clear(route[at],route[next+1]))next++;simplified.Add(route[next]);at=next;}
        return simplified.Take(512).ToArray();
    }
}
