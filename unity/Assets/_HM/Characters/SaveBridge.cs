using HM.Core;
using UnityEngine;

namespace HM.Characters
{
    /// <summary>Copies live scene state (clock, player position) into and out of SaveData.</summary>
    public static class SaveBridge
    {
        public static SaveData Capture(GameClock clock, Transform player, int gold = 0)
        {
            var p = player.position;
            return new SaveData { totalMinutes = clock.TotalMinutes, gold = gold, playerX = p.x, playerY = p.y, playerZ = p.z };
        }

        public static void Apply(SaveData data, GameClock clock, Transform player)
        {
            clock.SetTotalMinutes(data.totalMinutes);
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; // a CharacterController overrides direct transform writes
            player.position = new Vector3(data.playerX, data.playerY, data.playerZ);
            if (cc != null) cc.enabled = true;
        }
    }
}
