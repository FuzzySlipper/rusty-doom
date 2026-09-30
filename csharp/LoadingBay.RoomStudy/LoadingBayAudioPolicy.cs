using System.Numerics;
using Rusty.Engine;

namespace LoadingBay.Game;

/// <summary>
/// E1M1 effects policy: the Sfx bus state and the WAD-derived weapon and door
/// sounds (<c>content/doom-e1m1/sounds</c>, see docs/source-provenance.md).
/// Only the room-study scene emits them; its doors open once and never close.
/// The product decides when a sound plays; the Engine realizes it.
/// </summary>
internal sealed class LoadingBayAudioPolicy : IDisposable
{
    private const string SoundRoot = "content/doom-e1m1/sounds/";
    private const float FullVolume = 1f;
    private const float NormalPitch = 1f;
    private const float CenterPan = 0f;
    /// <summary>Weapon sounds come from the player, so they are not positioned.</summary>
    private const float PlayerSpatialBlend = 0f;
    private const float PlayerAttenuation = 1f;
    /// <summary>Door sounds come from the door; Doom's 1200-unit sound range at 16 units per metre.</summary>
    private const float WorldSpatialBlend = 1f;
    private const float WorldAttenuation = 75f;
    private readonly IAudioService _audio;
    private readonly AudioBusReadout _readout;
    private readonly AudioClip _pistol;
    private readonly AudioClip _shotgun;
    private readonly AudioClip _punch;
    private readonly AudioClip _doorOpen;
    private ulong _nextSignal;

    internal LoadingBayAudioPolicy(IAudioService audio, LoadingBayTuning tuning)
    {
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
        // First-party tuning and Engine delivery are trusted. The bus readout
        // remains for the HUD; no round-trip revalidation.
        _audio.SetBusVolume(new AudioBusVolumeRequest(AudioBus.Sfx, tuning.EffectsVolume));
        _audio.SetBusMuted(new AudioBusMutedRequest(AudioBus.Sfx, tuning.EffectsMuted));
        _readout = _audio.ReadBus(new AudioBusReadRequest(AudioBus.Sfx));
        List<AudioClip> opened = [];
        AudioClip Open(string lump)
        {
            AudioClip clip = _audio.OpenClip(new AudioClipRequest(SoundRoot + lump + ".wav"));
            opened.Add(clip);
            return clip;
        }
        try
        {
            _pistol = Open("DSPISTOL");
            _shotgun = Open("DSSHOTGN");
            _punch = Open("DSPUNCH");
            _doorOpen = Open("DSDOROPN");
        }
        catch
        {
            foreach (AudioClip clip in opened) clip.Dispose();
            throw;
        }
    }

    internal AudioBusReadout Readout => _readout;

    /// <summary>A pistol or shotgun shot. The fist is silent unless it lands, as in Doom.</summary>
    internal void WeaponFired(RecipeWeapon weapon)
    {
        if (weapon == RecipeWeapon.Pistol) EmitFromPlayer(_pistol);
        else if (weapon == RecipeWeapon.Shotgun) EmitFromPlayer(_shotgun);
    }

    internal void PunchLanded() => EmitFromPlayer(_punch);

    internal void DoorOpening(Vector3 position) => EmitAt(_doorOpen, position);

    private void EmitFromPlayer(AudioClip clip)
        => Emit(clip, PlayerSpatialBlend, PlayerAttenuation, AudioEmitterKind.Global2d, Vector3.Zero);

    private void EmitAt(AudioClip clip, Vector3 position)
        => Emit(clip, WorldSpatialBlend, WorldAttenuation, AudioEmitterKind.World3d, position);

    private void Emit(AudioClip clip, float spatialBlend, float attenuation, AudioEmitterKind kind, Vector3 position)
        => _audio.Emit(new AudioEmitRequest($"loading-bay.sfx.{_nextSignal++}", new AudioSourceDescriptor(
            clip, AudioBus.Sfx, FullVolume, NormalPitch, false, spatialBlend, attenuation, CenterPan,
            kind, position, 0, Vector3.Zero)));

    public void Dispose()
    {
        _pistol.Dispose();
        _shotgun.Dispose();
        _punch.Dispose();
        _doorOpen.Dispose();
    }
}
