using UnityEngine;
namespace Forger;

internal static class RolePhysics
{
    // Room and interaction triggers describe areas; they are not solid walls.
    internal static bool Blocked(Vector2 from,Vector2 to)=>Physics2D.LinecastAll(from,to,Constants.ShipAndObjectsMask)
        .Any(hit=>hit.collider&&!hit.collider.isTrigger);
    internal static bool Occupied(Vector2 point,float radius)=>Physics2D.OverlapCircleAll(point,radius,Constants.ShipAndObjectsMask)
        .Any(c=>c&&!c.isTrigger);
}
