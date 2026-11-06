using System;

namespace Foundry
{
    [Serializable]
    public sealed class WeaponSpec
    {
        public string Name;
        public int Magazine, Reserve, Damage;
        public float Interval, ReloadSeconds, Range, Recoil, Spread;
        public bool Automatic;
        public WeaponSpec(string name, int magazine, int reserve, int damage, float interval,
            float reload, float range, float recoil, float spread, bool automatic)
        {
            Name=name; Magazine=magazine; Reserve=reserve; Damage=damage; Interval=interval;
            ReloadSeconds=reload; Range=range; Recoil=recoil; Spread=spread; Automatic=automatic;
        }
    }

    // No Unity dependency: the same rules can be exercised by the standalone checks.
    public static class WeaponRules
    {
        public static readonly WeaponSpec[] All = {
            new WeaponSpec("M16A1",30,150,25,.1f,2.1f,120,1.15f,.004f,true),
            new WeaponSpec("DESERT EAGLE",7,42,55,.38f,1.6f,90,3.4f,.002f,false)
        };
        public static int ReloadAmount(int magazine, int reserve, int capacity) =>
            Math.Max(0, Math.Min(capacity - magazine, reserve));
        public static bool CanFire(int ammo, double now, double nextShot, bool reloading, bool alive) =>
            alive && !reloading && ammo > 0 && now >= nextShot;
        public static int Damage(int baseDamage, bool headshot) => headshot ? baseDamage * 2 : baseDamage;
    }
}
