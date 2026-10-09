using UnityEngine;
namespace Forger;
internal static class RoomLookup {
 internal static string Locate(PlayerControl player){
  var room=Game.Rooms().FirstOrDefault(r=>r.roomArea.OverlapPoint(player.GetTruePosition()));
  return room is not null && room ? Game.RoomName((byte)room.RoomId) : Game.RoomName((byte)SystemTypes.Hallway);
 }
}
