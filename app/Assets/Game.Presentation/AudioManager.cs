// Audio: positional buzz per fly (pitch/volume by speed and kind, pan by position; up to two slots), impacts per tool, UI,
// combos (pitch rises per level), extra time, doppler "escaped", take-off, looped music and kitchen ambience. All switchable (sound and music separately).
using UnityEngine;

namespace ThatDamnFly.Presentation
{
    public sealed class AudioManager
    {
        public const int BuzzSlots = 3;
        readonly AudioSource[] _buzz = new AudioSource[BuzzSlots];
        readonly AudioSource _fx, _ui, _pitched, _music, _amb, _layer;   // ADR-016: _layer = percussion that rises with fury
        AudioClip _heart; float _fury01, _layerVol;
        readonly AudioClip _tap, _fury, _win, _timeout, _tick, _whoosh, _catch, _flyIn, _combo, _bonus, _escape, _takeoff, _star, _bossIn, _bait, _slow, _ouch, _pop;
        readonly System.Collections.Generic.Dictionary<string, AudioClip> _impacts = new System.Collections.Generic.Dictionary<string, AudioClip>();
        readonly System.Collections.Generic.Dictionary<string, AudioClip> _ambiences = new System.Collections.Generic.Dictionary<string, AudioClip>();
        AudioClip _musicIndoor, _musicOutdoor;
        public bool Enabled = true;
        bool _musicEnabled = true; bool _unlocked;
        public bool MusicEnabled { get => _musicEnabled; set { _musicEnabled = value; ApplyMusic(); } }
        float _musicTarget = 0.35f;

        public AudioManager(GameObject host)
        {
            var buzzClip = Resources.Load<AudioClip>("audio/buzz_loop");
            for (int i = 0; i < BuzzSlots; i++) { var b = host.AddComponent<AudioSource>(); b.loop = true; b.playOnAwake = false; b.clip = buzzClip; b.volume = 0f; _buzz[i] = b; }
            _fx = host.AddComponent<AudioSource>(); _fx.playOnAwake = false;
            _ui = host.AddComponent<AudioSource>(); _ui.playOnAwake = false;
            _pitched = host.AddComponent<AudioSource>(); _pitched.playOnAwake = false;
            _music = host.AddComponent<AudioSource>(); _music.loop = true; _music.playOnAwake = false; _music.clip = Resources.Load<AudioClip>("audio/music_loop"); _music.volume = 0f;
            _amb = host.AddComponent<AudioSource>(); _amb.loop = true; _amb.playOnAwake = false; _amb.volume = 0f;
            _ambiences["kitchen"] = Resources.Load<AudioClip>("audio/ambience_loop"); _amb.clip = _ambiences["kitchen"];
            _musicIndoor = _music.clip; _musicOutdoor = Resources.Load<AudioClip>("audio/music_picnic") ?? _music.clip;
            _layer = host.AddComponent<AudioSource>(); _layer.loop = true; _layer.playOnAwake = false; _layer.clip = Resources.Load<AudioClip>("audio/music_layer"); _layer.volume = 0f; _heart = Resources.Load<AudioClip>("audio/heartbeat");
            _tap = Resources.Load<AudioClip>("audio/ui_tap"); _fury = Resources.Load<AudioClip>("audio/fury_up"); _win = Resources.Load<AudioClip>("audio/win"); _timeout = Resources.Load<AudioClip>("audio/timeout"); _tick = Resources.Load<AudioClip>("audio/tick"); _whoosh = Resources.Load<AudioClip>("audio/whoosh"); _catch = Resources.Load<AudioClip>("audio/catch"); _flyIn = Resources.Load<AudioClip>("audio/fly_in");
            _combo = Resources.Load<AudioClip>("audio/combo"); _bonus = Resources.Load<AudioClip>("audio/bonus"); _escape = Resources.Load<AudioClip>("audio/escape"); _takeoff = Resources.Load<AudioClip>("audio/takeoff");
            _star = Resources.Load<AudioClip>("audio/star"); _bossIn = Resources.Load<AudioClip>("audio/boss_in"); _bait = Resources.Load<AudioClip>("audio/bait"); _slow = Resources.Load<AudioClip>("audio/slow"); _ouch = Resources.Load<AudioClip>("audio/ouch"); _pop = Resources.Load<AudioClip>("audio/pop");
        }

        /// <summary>Call after the user's first gesture (web): starts the loops.</summary>
        public void Unlock()
        {
            _unlocked = true;
            foreach (var b in _buzz) if (b.clip != null && !b.isPlaying) b.Play();
            if (_music.clip != null && !_music.isPlaying) _music.Play();
            if (_layer.clip != null && !_layer.isPlaying) { _layer.Play(); _layer.timeSamples = _music.timeSamples; }
            if (_amb.clip != null && !_amb.isPlaying) _amb.Play();
            ApplyMusic();
        }

        void ApplyMusic() { if (!_unlocked) return; _music.volume = (Enabled && _musicEnabled) ? _musicTarget : 0f; }
        /// <summary>Target music volume: 1 on start/result, lower during the round.</summary>
        public void SetMusicLevel(float level) { _musicTarget = 0.35f * level; _fury01 = 0f; ApplyMusic(); }
        /// <summary>ADR-016: the percussion layer follows fury (0 below 20, full at 70); smoothed in Frame.</summary>
        public void SetFury(int fury) { _fury01 = Mathf.Clamp01((fury - 20) / 50f); }
        public void Frame(float dt)
        {
            float target = (Enabled && _musicEnabled && _unlocked) ? _musicTarget * 1.1f * _fury01 : 0f;
            _layerVol = Mathf.MoveTowards(_layerVol, target, dt * 0.6f); if (_layer.volume != _layerVol) _layer.volume = _layerVol;
            if (_layer.isPlaying && _music.isPlaying && Mathf.Abs(_layer.timeSamples - _music.timeSamples) > 2048) _layer.timeSamples = _music.timeSamples;   // keeps both in phase
        }
        public void Heartbeat(float pitch) { if (Enabled && _heart != null) { _pitched.pitch = pitch; _pitched.PlayOneShot(_heart, 0.55f); } }
        public void SetAmbience(bool on) { _amb.volume = (Enabled && on) ? 0.22f : 0f; }
        /// <summary>Scene ambience (kitchen: fridge + clock; picnic: breeze + birds).</summary>
        public void SetScene(string sceneId, bool outdoor)
        {
            if (!_ambiences.TryGetValue(sceneId, out var clip)) { clip = Resources.Load<AudioClip>("audio/ambience_" + sceneId); _ambiences[sceneId] = clip; }
            if (clip != null && _amb.clip != clip) { bool was = _amb.isPlaying; _amb.Stop(); _amb.clip = clip; if (was || _unlocked) _amb.Play(); }
            var m = outdoor ? _musicOutdoor : _musicIndoor;
            if (m != null && _music.clip != m) { bool was = _music.isPlaying; _music.Stop(); _music.clip = m; if (was || _unlocked) { _music.Play(); if (_layer.clip != null) { _layer.Stop(); _layer.Play(); _layer.timeSamples = _music.timeSamples; } } ApplyMusic(); }
        }

        public void SetBuzz(int slot, bool active, float speed, float x01, float pitchBase)
        {
            var b = _buzz[slot];
            if (!Enabled || !active) { b.volume = Mathf.MoveTowards(b.volume, 0f, 0.1f); return; }
            b.volume = Mathf.MoveTowards(b.volume, 0.22f + 0.22f * Mathf.Clamp01(speed / 1.2f), 0.05f);
            b.pitch = pitchBase * (0.9f + 0.5f * Mathf.Clamp01(speed / 1.2f));
            b.panStereo = Mathf.Lerp(-0.6f, 0.6f, x01);
        }
        public void StopBuzz() { foreach (var b in _buzz) b.volume = 0f; }
        public void Impact(string toolId)
        {
            if (!Enabled) return;
            if (!_impacts.TryGetValue(toolId, out var c)) { c = Resources.Load<AudioClip>("audio/impact_" + toolId.Substring(toolId.IndexOf('_') + 1)); _impacts[toolId] = c; }
            if (c != null) _fx.PlayOneShot(c, 0.9f);
        }
        public void Whoosh() { if (Enabled && _whoosh != null) _fx.PlayOneShot(_whoosh, 0.5f); }
        public void Tap() { if (Enabled && _tap != null) _ui.PlayOneShot(_tap, 0.6f); }
        public void FuryUp() { if (Enabled && _fury != null) _ui.PlayOneShot(_fury, 0.6f); }
        public void Win() { StopBuzz(); if (Enabled && _win != null) _ui.PlayOneShot(_win, 0.8f); }
        public void Timeout() { if (Enabled && _timeout != null) _ui.PlayOneShot(_timeout, 0.6f); }
        public void Catch(float pitch = 1f) { if (Enabled && _catch != null) { _pitched.pitch = pitch; _pitched.PlayOneShot(_catch, 1f); } }
        public void FlyIn(float pitch = 1f) { if (Enabled && _flyIn != null) { _pitched.pitch = pitch; _pitched.PlayOneShot(_flyIn, 0.6f); } }
        public void Combo(int level) { if (Enabled && _combo != null) { _pitched.pitch = 1f + 0.12f * Mathf.Clamp(level - 2, 0, 6); _pitched.PlayOneShot(_combo, 0.7f); } }
        public void Bonus() { if (Enabled && _bonus != null) _ui.PlayOneShot(_bonus, 0.5f); }
        /// <summary>ADR-014: request fulfilled (chime) and boss fly entry (low buzz).</summary>
        public void Star() { if (Enabled && _star != null) _ui.PlayOneShot(_star, 0.7f); }
        public void BossIn() { if (Enabled && _bossIn != null) _ui.PlayOneShot(_bossIn, 0.8f); }
        /// <summary>ADR-015: bait placed (sweet plop) and slow-motion entry (descent).</summary>
        public void Bait() { if (Enabled && _bait != null) _ui.PlayOneShot(_bait, 0.6f); }
        public void SlowMo() { if (Enabled && _slow != null) _ui.PlayOneShot(_slow, 0.5f); }
        /// <summary>ADR-019: the character's "ouch" (pitch varies with fury) and burst.</summary>
        public void Ouch(float pitch) { if (Enabled && _ouch != null) { _pitched.pitch = pitch; _pitched.PlayOneShot(_ouch, 0.7f); } }
        public void Pop() { if (Enabled && _pop != null) _ui.PlayOneShot(_pop, 0.9f); }
        public void Escape(float pitch = 1f) { if (Enabled && _escape != null) { _pitched.pitch = pitch; _pitched.PlayOneShot(_escape, 0.7f); } }
        public void Takeoff(float pitch = 1f) { if (Enabled && _takeoff != null) { _pitched.pitch = pitch; _pitched.PlayOneShot(_takeoff, 0.5f); } }
        public void Tick() { if (Enabled && _tick != null) _ui.PlayOneShot(_tick, 0.5f); }
        /// <summary>Call when "Sound" changes: mutes/restores the loops.</summary>
        public void Refresh() { ApplyMusic(); if (!Enabled) { StopBuzz(); _amb.volume = 0f; } }
    }
}
