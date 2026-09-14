using LoadingBay.Game;

internal static class RecipeAnimationExercise
{
    internal static void Run()
    {
        var fist = LoadingBayRecipeAnimation.Fist;
        var pistol = LoadingBayRecipeAnimation.Pistol;
        var shotgun = LoadingBayRecipeAnimation.Shotgun;
        static void Check(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
        Check(LoadingBayRecipeAnimation.Idle(RecipeWeapon.Fist) == "PUNGA0", "Fist ready pose missing");
        Check(LoadingBayRecipeAnimation.At(fist, 0) == "PUNGB0" && LoadingBayRecipeAnimation.At(fist, 4.0/35) == "PUNGC0" && LoadingBayRecipeAnimation.At(fist, 8.0/35) == "PUNGD0", "Punch must reach all forward poses");
        Check(LoadingBayRecipeAnimation.At(fist, 13.0/35) == "PUNGC0" && LoadingBayRecipeAnimation.At(fist, 17.0/35) == "PUNGB0" && LoadingBayRecipeAnimation.At(fist, 22.0/35) is null, "Punch must recover to ready");
        Check(LoadingBayRecipeAnimation.At(pistol, 0) == "PISGA0", "Pistol initial windup missing");
        Check(LoadingBayRecipeAnimation.At(pistol, 4.0/35) == "PISGB0", "Pistol discharge boundary differs from source timing");
        Check(LoadingBayRecipeAnimation.At(pistol, 10.0/35) == "PISGC0", "Pistol recoil frame missing");
        Check(LoadingBayRecipeAnimation.At(pistol, 19.0/35) is null, "Pistol did not settle after recovery");
        Check(LoadingBayRecipeAnimation.At(shotgun, 20.0/35) == "SHTGD0", "Shotgun pump apex missing");
        Check(LoadingBayRecipeAnimation.At(shotgun, 24.0/35) == "SHTGC0", "Shotgun reverse pump missing");
        Check(LoadingBayRecipeAnimation.At(shotgun, 44.0/35) is null, "Shotgun did not finish its complete cycle");
        Check(LoadingBayRecipeAnimation.At(LoadingBayRecipeAnimation.ShotgunFlash, -.001) is null, "Flash appeared before discharge");
        Check(LoadingBayRecipeAnimation.At(LoadingBayRecipeAnimation.ShotgunFlash, 4.0/35) == "SHTFB0", "Second muzzle flash frame missing");
        Check(LoadingBayRecipeAnimation.At(LoadingBayRecipeAnimation.ShotgunFlash, 7.0/35) is null, "Muzzle flash failed to expire");
        var imp = new RecipeEnemy(1, default, true) { Health = 0, DeathStarted = 2 };
        Check(imp.Sprite(2) == "TROOI0" && imp.Sprite(2+16.0/35) == "TROOK0" && imp.Sprite(20) == "TROOM0", "Imp death must animate before retaining the corpse");
        var guard = new RecipeEnemy(2, default, false) { Health = 0, DeathStarted = 2 };
        Check(guard.Sprite(2) == "POSSH0" && guard.Sprite(2+10.0/35) == "POSSJ0" && guard.Sprite(20) == "POSSL0", "Trooper death must animate before retaining the corpse");
        foreach (var frame in fist.Concat(pistol).Concat(shotgun).Concat(LoadingBayRecipeAnimation.PistolFlash).Concat(LoadingBayRecipeAnimation.ShotgunFlash))
        {
            var sprite = LoadingBayRecipeSprites.Frame("view/" + frame.Name);
            Check(sprite.Size == new System.Numerics.Vector2(10, 6.3f), "Weapon frames must share one canvas and pixel aspect");
        }
        Console.WriteLine("Recipe weapon/actor animation exercise passed.");
    }
}
