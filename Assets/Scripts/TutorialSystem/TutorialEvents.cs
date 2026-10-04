using System;

namespace Tutorial
{
    public static class TutorialEvents
    {
        public const string SwitchUsed = "SwitchUsed";
        public const string ChestOpened = "ChestOpened";
        public const string ItemPickedUp = "ItemPickedUp";
        public const string WeaponPickedUp = "WeaponPickedUp";
        public const string GearPickedUp = "GearPickedUp";
        public const string GemPickedUp = "GemPickedUp";
        public const string KeyPickedUp = "KeyPickedUp";
        public const string LorePickedUp = "LorePickedUp";
        public const string PickedUpPrefix = "PickedUp:";

        public const string PlayerJumped = "PlayerJumped";
        public const string PlayerDashed = "PlayerDashed";
        public const string PlayerBackDashed = "PlayerBackDashed";
        public const string PlayerWallSlid = "PlayerWallSlid";
        public const string PlayerWallJumped = "PlayerWallJumped";
        public const string PlayerClimbed = "PlayerClimbed";
        public const string PlayerHealed = "PlayerHealed";
        public const string PlayerHit = "PlayerHit";
        public const string PlayerDroppedThrough = "PlayerDroppedThrough";

        public const string EnemyHit = "EnemyHit";
        public const string EnemyComboFinished = "EnemyComboFinished";
        public const string EnemyCrit = "EnemyCrit";
        public const string EnemySkillHit = "EnemySkillHit";
        public const string EnemyReflectHit = "EnemyReflectHit";
        public const string EnemyKilled = "EnemyKilled";

        public static event Action<string> OnRaised;

        public static void Raise(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            OnRaised?.Invoke(key);
        }
    }
}