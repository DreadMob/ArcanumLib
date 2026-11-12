#nullable enable
using System;
using Vintagestory.API.Common;

namespace ArcanumLib.Entities;

/// <summary>
/// A looping clip whose speed follows something on the client: wheels, paddles, treads, a drill head, a winch drum.
/// Started once and never stopped: standing still freezes it at speed 0 where it is. Stopping it would fade the bones
/// back to their rest angle (wheels visibly roll backwards for a moment) and the next start would jump to frame 0.
/// Started client-side (<c>ClientSide = true</c>), so the server's animation syncs (mount, dismount, gear change) cannot
/// strip it — a client clip without that flag is dropped by the next server animation packet on any entity but the
/// local player; if anything still drops it, it resumes from the frame it was on. The speed is eased (~0.15 s):
/// observers get it in steps from the server, and every step would show as a jerk. Call <see cref="Run"/> every client
/// frame or tick; never <c>StopAnimation</c> the clip.
/// </summary>
public sealed class WheelClip
{
    private readonly string _code;
    private float _frame;
    private float _speed;
    private const float EaseTau = 0.15f;

    public WheelClip(string code) => _code = code;

    /// <summary>The eased clip speed last applied (0 = holding still).</summary>
    public float Speed => _speed;
    /// <summary>The frame the clip was on at the last <see cref="Run"/>.</summary>
    public float Frame => _frame;

    /// <param name="target">Clip speed, signed (negative runs it backwards); 0 = come to rest and hold the pose.</param>
    /// <returns>True when the clip was (re)started this call.</returns>
    public bool Run(IAnimationManager mgr, float target, float dt)
    {
        if (mgr.Animator == null) return false;
        _speed += (target - _speed) * (1f - MathF.Exp(-Math.Max(0f, dt) / EaseTau));
        if (target == 0f && MathF.Abs(_speed) < 0.03f) _speed = 0f;
        float speed = _speed;
        var state = mgr.Animator.GetAnimationState(_code);
        if (mgr.ActiveAnimationsByAnimCode.TryGetValue(_code, out var meta) && state?.Active == true)
        {
            meta.AnimationSpeed = speed;
            if (state.meta != null) state.meta.AnimationSpeed = speed;
            _frame = state.CurrentFrame;
            return false;
        }
        if (speed == 0f) return false;                      // never ran (or dropped while standing): rest pose is fine
        mgr.StartAnimation(new AnimationMetaData
        {
            Animation = _code, Code = _code, ClientSide = true, AnimationSpeed = speed,
            // easing runs at the clip's speed: at 4 a crawling machine showed its wheels and beam only half turned
            EaseInSpeed = 20f, EaseOutSpeed = 4f, StartFrameOnce = _frame
        }.Init());
        return true;
    }
}
