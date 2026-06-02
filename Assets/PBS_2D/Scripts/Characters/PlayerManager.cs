using System;
using UnityEngine;

namespace PBS2D
{
    public static class PlayerManager
    {
        public static Character Player { get; private set; }

        public static event Action<Character> OnPlayerSpawned;
        public static event Action<Character> OnPlayerDespawned;

        // Reset static state when entering play mode with domain reload disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Player = null;
            OnPlayerSpawned = null;
            OnPlayerDespawned = null;
        }

        public static void Register(Character player)
        {
            if (Player == player) return;

            Player = player;
            OnPlayerSpawned?.Invoke(player);
        }

        public static void Unregister(Character player)
        {
            if (Player != player) return;

            Player = null;
            OnPlayerDespawned?.Invoke(player);
        }
    }
}
