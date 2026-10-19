namespace Sandglass.Game
{
    /// <summary>Client copy of the server's weapon table (server/internal/game/weapons.go).</summary>
    public struct WeaponInfo
    {
        public string Name;
        public float Interval, Reload;
        public int Mag, Reserve;
        public bool Auto;
        public float HipSpread, AdsSpread, Bloom, RecoilUp, RecoilSide, Kick;
    }

    public static class Weapons
    {
        public static readonly WeaponInfo[] All =
        {
            new WeaponInfo { Name = "RIFLE", Interval = 0.1f, Reload = 2.2f, Mag = 30, Reserve = 90, Auto = true,
                HipSpread = 1.1f, AdsSpread = 0.12f, Bloom = 0.28f, RecoilUp = 0.55f, RecoilSide = 0.22f, Kick = 0.6f },
            new WeaponInfo { Name = "PISTOL", Interval = 0.22f, Reload = 1.5f, Mag = 12, Reserve = 48, Auto = false,
                HipSpread = 0.8f, AdsSpread = 0.15f, Bloom = 0.5f, RecoilUp = 1.4f, RecoilSide = 0.35f, Kick = 1f },
        };
    }
}
