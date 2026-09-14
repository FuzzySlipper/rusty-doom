namespace LoadingBay.Game;

/// <summary>Doom presentation timing in source 35 Hz tics, sampled on Engine-admitted time.</summary>
internal enum RecipeWeapon { Fist, Pistol, Shotgun }

internal static class LoadingBayRecipeAnimation
{
    internal const double Tic = 1.0 / 35;
    internal readonly record struct Frame(string Name, int Tics);
    internal static readonly Frame[] Fist = [new("PUNGB0",4), new("PUNGC0",4), new("PUNGD0",5), new("PUNGC0",4), new("PUNGB0",5)];
    internal static readonly Frame[] Pistol = [new("PISGA0",4), new("PISGB0",6), new("PISGC0",4), new("PISGB0",5)];
    internal static readonly Frame[] Shotgun = [new("SHTGA0",3), new("SHTGA0",7), new("SHTGB0",5), new("SHTGC0",5), new("SHTGD0",4), new("SHTGC0",5), new("SHTGB0",5), new("SHTGA0",3), new("SHTGA0",7)];
    internal static readonly Frame[] PistolFlash = [new("PISFA0",7)];
    internal static readonly Frame[] ShotgunFlash = [new("SHTFA0",4), new("SHTFB0",3)];
    internal static readonly Frame[] TrooperAttack = [new("POSSE1",10), new("POSSF1",8), new("POSSE1",8)];
    internal static readonly Frame[] ImpAttack = [new("TROOE1",8), new("TROOF1",8), new("TROOG1",6)];
    internal static readonly Frame[] TrooperDeath = [new("POSSH0",5), new("POSSI0",5), new("POSSJ0",5), new("POSSK0",5), new("POSSL0",5)];
    internal static readonly Frame[] ImpDeath = [new("TROOI0",8), new("TROOJ0",8), new("TROOK0",6), new("TROOL0",6), new("TROOM0",6)];
    internal static readonly Frame[] FireballImpact = [new("BAL1C0",6), new("BAL1D0",6), new("BAL1E0",6)];
    internal static double Duration(Frame[] frames) => frames.Sum(f => f.Tics) * Tic;
    internal static string? At(Frame[] frames, double elapsed, bool holdLast = false)
    {
        if (elapsed < 0) return null;
        double end = 0;
        foreach (var frame in frames)
        {
            end += frame.Tics * Tic;
            if (elapsed + 1e-9 < end) return frame.Name;
        }
        return holdLast ? frames[^1].Name : null;
    }
    internal static Frame[] Frames(RecipeWeapon weapon) => weapon switch { RecipeWeapon.Fist => Fist, RecipeWeapon.Shotgun => Shotgun, _ => Pistol };
    internal static string Idle(RecipeWeapon weapon) => weapon switch { RecipeWeapon.Fist => "PUNGA0", RecipeWeapon.Shotgun => "SHTGA0", _ => "PISGA0" };
    internal static double FireDelay(RecipeWeapon weapon) => (weapon == RecipeWeapon.Shotgun ? 3 : 4) * Tic;
    internal static double FireDelay(bool shotgun) => (shotgun ? 3 : 4) * Tic;
}
