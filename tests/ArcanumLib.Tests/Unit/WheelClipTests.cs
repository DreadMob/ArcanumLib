using System.Collections.Generic;
using ArcanumLib.Entities;
using NSubstitute;
using Vintagestory.API.Common;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class WheelClipTests
{
    private sealed class Rig
    {
        public readonly IAnimationManager Manager = Substitute.For<IAnimationManager>();
        public readonly IAnimator Animator = Substitute.For<IAnimator>();
        public readonly Dictionary<string, AnimationMetaData> Active = new();
        public readonly RunningAnimation State = new();
        public readonly List<AnimationMetaData> Started = new();

        public Rig()
        {
            Manager.Animator.Returns(Animator);
            Manager.ActiveAnimationsByAnimCode.Returns(Active);
            Animator.GetAnimationState("roll").Returns(State);
            Manager.StartAnimation(Arg.Any<AnimationMetaData>()).Returns(ci =>
            {
                var meta = ci.Arg<AnimationMetaData>();
                Started.Add(meta);
                Active[meta.Code] = meta;
                State.Active = true;
                State.meta = meta;
                return true;
            });
        }
    }

    [Fact]
    public void Standing_Still_Never_Starts_The_Clip()
    {
        var rig = new Rig();
        var clip = new WheelClip("roll");
        Assert.False(clip.Run(rig.Manager, 0f, 0.05f));
        Assert.Empty(rig.Started);
    }

    [Fact]
    public void Starts_Client_Side_And_Eases_The_Speed()
    {
        var rig = new Rig();
        var clip = new WheelClip("roll");
        Assert.True(clip.Run(rig.Manager, 2f, 0.05f));
        var meta = Assert.Single(rig.Started);
        Assert.True(meta.ClientSide);
        Assert.InRange(meta.AnimationSpeed, 0.01f, 1.99f);

        Assert.False(clip.Run(rig.Manager, 2f, 1f));
        Assert.Single(rig.Started);
        Assert.InRange(rig.Active["roll"].AnimationSpeed, 1.9f, 2f);
    }

    [Fact]
    public void Coming_To_Rest_Holds_The_Clip_At_Speed_Zero()
    {
        var rig = new Rig();
        var clip = new WheelClip("roll");
        clip.Run(rig.Manager, 2f, 1f);
        for (int i = 0; i < 40; i++) clip.Run(rig.Manager, 0f, 0.1f);
        Assert.Equal(0f, clip.Speed);
        Assert.Equal(0f, rig.Active["roll"].AnimationSpeed);
        Assert.Single(rig.Started);
        rig.Manager.DidNotReceive().StopAnimation(Arg.Any<string>());
    }

    [Fact]
    public void A_Dropped_Clip_Resumes_From_Its_Frame()
    {
        var rig = new Rig();
        var clip = new WheelClip("roll");
        clip.Run(rig.Manager, 1f, 1f);
        rig.State.CurrentFrame = 17f;
        clip.Run(rig.Manager, 1f, 0.05f);
        Assert.Equal(17f, clip.Frame);

        rig.State.Active = false;          // a server animation packet stripped it
        rig.Active.Clear();
        Assert.True(clip.Run(rig.Manager, 1f, 0.05f));
        Assert.Equal(17f, rig.Started[^1].StartFrameOnce);
    }
}
