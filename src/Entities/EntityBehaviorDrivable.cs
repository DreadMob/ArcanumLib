using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ArcanumLib.Entities;

/// <summary>
/// Generic entity behavior that lets the passenger of a controllable <see cref="IMountable" />
/// seat drive the entity. Each tick it reads the controlling seat's synchronized
/// <see cref="EntityControls" />, integrates a signed forward speed and a yaw rate, rotates
/// <see cref="EntityPos.Yaw" /> and writes the horizontal part of <see cref="EntityPos.Motion" />
/// along the entity facing, using the same convention as vanilla controllable mounts
/// (forward is <c>(sin(yaw), 0, cos(yaw))</c>). <see cref="Vec3d.Y" /> of the motion is never
/// touched so any physics behavior keeps full authority over the vertical axis.
/// <br /><br />
/// The behavior only computes desired motion; the actual terrain integration is done by a
/// physics behavior such as <c>passivephysicsmultibox</c>. While a player occupies the
/// controlling seat, vanilla physics deliberately skips server-side integration and the
/// controlling player's client becomes position-authoritative - this behavior keeps
/// <see cref="EntityBehaviorSeatable.Controller" /> up to date so that pipeline engages.
/// When the controlling passenger is not a player, no client simulates the entity, so this
/// behavior additionally integrates the horizontal motion itself on the server.
/// <br /><br />
/// Register it with <c>api.RegisterEntityBehaviorClass("drivable", typeof(EntityBehaviorDrivable))</c>
/// on both sides and add it to the entity's server and client behavior lists.
/// </summary>
/// <example>
/// <code language="json">
/// { "code": "drivable", "drive": { "maxSpeed": 5, "turnSpeed": 1.2, "waterOnly": true } }
/// </code>
/// </example>
public class EntityBehaviorDrivable : EntityBehavior
{
    /// <summary>Interval in seconds between watched-attribute writes (10 Hz).</summary>
    private const float SyncIntervalSeconds = 0.1f;

    /// <summary>Actual displacement below this fraction of the expected one counts as a stall.</summary>
    private const double StallDisplacementRatio = 0.35;

    /// <summary>Consecutive stalled ticks required before <see cref="IsBlocked" /> is raised.</summary>
    private const int StallTicksToBlock = 2;

    /// <summary>Speeds at or below this magnitude are considered standstill.</summary>
    private const float StandstillSpeed = 0.005f;

    /// <summary>Minimum interval in seconds between two step-up assists.</summary>
    private const float StepUpCooldownSeconds = 0.25f;

    /// <summary>Watched-attribute key holding <see cref="CurrentSpeed" />.</summary>
    private const string SpeedAttributeKey = "drivableSpeed";

    /// <summary>Watched-attribute key holding <see cref="YawRate" />.</summary>
    private const string YawAttributeKey = "drivableYaw";

    /// <summary>Watched-attribute key holding the engine master switch —
    /// persisted (WatchedAttributes serialize) and synced to every client.</summary>
    private const string EngineAttributeKey = "drivableEngineOn";

    private DrivableParams _params = new();
    private IMountable? _mountable;
    private EntityBehaviorPassivePhysicsMultiBox? _multiBoxPhysics;
    private float _boxesAlignedYaw = float.NaN;

    private float _currentSpeed;
    private float _yawRate;
    private float _throttleInput;
    private float _steerInput;
    private float _maxSpeedFactor = 1f;
    private bool _isBlocked;

    private double _lastX;
    private double _lastZ;
    private double _expectedDisplacement;
    private int _stallTicks;
    private float _stepUpCooldown;
    private float _climbTimer;
    private float _syncAccum;
    private Vec3d _integrationResult = new();

    /// <summary>
    /// Creates the behavior. Standard entity behavior constructor signature required by
    /// <c>api.RegisterEntityBehaviorClass</c>.
    /// </summary>
    /// <param name="entity">The entity this behavior is attached to.</param>
    public EntityBehaviorDrivable(Entity entity) : base(entity)
    {
    }

    /// <summary>
    /// The behavior storage name: <c>drivable</c>.
    /// </summary>
    /// <returns>The constant <c>drivable</c>.</returns>
    public override string PropertyName() => "drivable";

    /// <summary>
    /// The resolved drive parameters, loaded from the <c>drive</c> object of this
    /// behavior's json configuration.
    /// </summary>
    public DrivableParams Params => _params;

    /// <summary>
    /// Signed forward speed in blocks per second; negative while reversing.
    /// On non-controlling clients this mirrors the synced watched attribute.
    /// </summary>
    public float CurrentSpeed => _currentSpeed;

    /// <summary>
    /// Currently demanded yaw rate in radians per second, before
    /// <see cref="DrivableParams.Understeer" /> losses are applied to the entity.
    /// Positive values turn left. On non-controlling clients this mirrors the synced
    /// watched attribute.
    /// </summary>
    public float YawRate => _yawRate;

    /// <summary>
    /// Raw longitudinal input this tick: +1 forward, -1 backward, 0 when neither,
    /// both, or no controller is present.
    /// </summary>
    public float ThrottleInput => _throttleInput;

    /// <summary>
    /// Raw steering input this tick: +1 left, -1 right, 0 when neither, both,
    /// or no controller is present.
    /// </summary>
    public float SteerInput => _steerInput;

    /// <summary>
    /// External speed multiplier applied to the target speed. 0..1 models reduced
    /// power or grip; values above 1 are allowed — an overdrive/boost channel
    /// legitimately raises the ceiling (hard-capped at 2.5 for sanity).
    /// Non-finite values are rejected.
    /// </summary>
    public float MaxSpeedFactor
    {
        get => _maxSpeedFactor;
        set => _maxSpeedFactor = float.IsFinite(value) ? GameMath.Clamp(value, 0f, 2.5f) : 1f;
    }

    /// <summary>
    /// True when the entity tried to move this tick but could not: either
    /// <see cref="DrivableParams.WaterOnly" /> is set and the entity's feet are not in a
    /// liquid, or forward motion is stalled by terrain.
    /// </summary>
    public bool IsBlocked => _isBlocked;

    /// <summary>
    /// Master engine switch — the throttle is dead while this is off and the
    /// vehicle coasts to a stop. Synced and persisted through the watched
    /// attributes; defaults to on so a freshly deployed vehicle can roll.
    /// </summary>
    public bool EngineOn => entity.WatchedAttributes.GetBool(EngineAttributeKey, true);

    /// <summary>True when <paramref name="who"/> sits in a seat that may
    /// control this entity — the driver, for engine-toggle purposes.</summary>
    public bool IsDriver(Entity? who)
    {
        if (who == null) return false;
        var mountable = _mountable ??= entity.GetInterface<IMountable>();
        var seats = mountable?.Seats;
        if (seats != null)
        {
            for (int i = 0; i < seats.Length; i++)
                if (seats[i] is { CanControl: true, Passenger: not null } seat
                    && ReferenceEquals(seat.Passenger, who))
                    return true;
        }
        return ReferenceEquals(mountable?.Controller, who);
    }

    /// <summary>Driver-only engine toggle. Server-side authority: returns
    /// false for anyone not in the controlling seat.</summary>
    public bool SetEngineOn(Entity? byEntity, bool on)
    {
        if (entity.World.Side != EnumAppSide.Server) return false;
        if (!IsDriver(byEntity)) return false;
        entity.WatchedAttributes.SetBool(EngineAttributeKey, on);
        entity.WatchedAttributes.MarkPathDirty(EngineAttributeKey);
        return true;
    }

    /// <summary>
    /// Loads <see cref="Params" /> from the <c>drive</c> object of the behavior configuration.
    /// </summary>
    /// <param name="properties">The properties of this entity.</param>
    /// <param name="attributes">The attributes of this entity behavior.</param>
    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        _params = DrivableParams.FromAttributes(attributes["drive"]);
    }

    /// <summary>
    /// Resolves cross-behavior references and restores the persisted drive state from
    /// the watched attributes.
    /// </summary>
    /// <param name="onFirstSpawn">True when the entity was just spawned instead of loaded.</param>
    public override void AfterInitialized(bool onFirstSpawn)
    {
        _mountable = entity.GetInterface<IMountable>();
        _multiBoxPhysics = entity.GetBehavior<EntityBehaviorPassivePhysicsMultiBox>();

        var wa = entity.WatchedAttributes;
        _currentSpeed = wa.GetFloat(SpeedAttributeKey);
        _yawRate = wa.GetFloat(YawAttributeKey);

        var pos = entity.Pos;
        _lastX = pos.X;
        _lastZ = pos.Z;
    }

    /// <summary>
    /// Per-tick drive update. On the server this is authoritative for non-player
    /// controllers; on the client it only runs while the local player occupies the
    /// controlling seat (client-authoritative mount physics). Other clients merely
    /// refresh the exposed values from the watched attributes.
    /// </summary>
    /// <param name="deltaTime">Elapsed time since the previous tick, in seconds.</param>
    public override void OnGameTick(float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime <= 0f) return;

        // Ignore lag spikes, same guard as the vanilla controllable entities use.
        float dt = MathF.Min(deltaTime, 0.5f);

        if (entity.State != EnumEntityState.Active) return;

        // Behaviors may be created before every sibling exists, so keep trying until found.
        _mountable ??= entity.GetInterface<IMountable>();

        bool server = entity.World.Side == EnumAppSide.Server;

        ResolveController(out Entity? controller, out EntityControls? controls);
        bool controlled = controller != null && controls != null;

        if (!server && (!controlled || controller is not EntityPlayer || !IsLocalPlayer(controller)))
        {
            // Remote observers and mounts controlled by someone else are packet-driven;
            // only keep the exposed values in sync.
            var wa = entity.WatchedAttributes;
            _currentSpeed = wa.GetFloat(SpeedAttributeKey, _currentSpeed);
            _yawRate = wa.GetFloat(YawAttributeKey, _yawRate);
            _throttleInput = 0f;
            _steerInput = 0f;
            _isBlocked = false;
            _expectedDisplacement = 0;
            _lastX = entity.Pos.X;
            _lastZ = entity.Pos.Z;
            return;
        }

        DriveTick(dt, server, controlled, controller, controls);

        if (server) SyncAttributes(dt);

        OnDriveTick(dt);
    }

    /// <summary>
    /// Called at the end of every tick that ran the drive update, on whichever side ran it.
    /// </summary>
    /// <param name="dt">Elapsed time since the previous tick, in seconds.</param>
    protected virtual void OnDriveTick(float dt)
    {
    }

    /// <summary>
    /// Finds the controlling seat and its controls. Also mirrors the controller entity into
    /// <see cref="EntityBehaviorSeatable.Controller" /> when the mountable is a vanilla-style
    /// seatable - vanilla physics and the client-side mount physics loop key off that property.
    /// </summary>
    private void ResolveController(out Entity? controller, out EntityControls? controls)
    {
        controller = null;
        controls = null;

        var mountable = _mountable;
        if (mountable == null) return;

        var seats = mountable.Seats;
        if (seats != null)
        {
            for (int i = 0; i < seats.Length; i++)
            {
                var seat = seats[i];
                if (seat is { CanControl: true, Passenger: not null })
                {
                    controller = seat.Passenger;
                    controls = seat.Controls;
                    break;
                }
            }
        }

        // Custom IMountable implementations may expose a controller without seats.
        controller ??= mountable.Controller;
        controls ??= mountable.ControllingControls;

        if (mountable is EntityBehaviorSeatable seatable && !ReferenceEquals(seatable.Controller, controller))
        {
            seatable.Controller = controller;
        }
    }

    /// <summary>
    /// The shared drive simulation: inputs, speed integration, yaw integration, motion
    /// write, blocked detection and the step-up assist.
    /// </summary>
    private void DriveTick(float dt, bool server, bool controlled, Entity? controller, EntityControls? controls)
    {
        var pos = entity.Pos;

        // Compare where the entity actually got moved to since the last tick with the
        // displacement the last tick's speed should have produced. Only meaningful on
        // the side that integrates the motion: while a player drives, the server just
        // receives position packets and must not count the idle ticks between them.
        bool locallyIntegrated = !server || !controlled || controller is not EntityPlayer;
        {
            double dx = pos.X - _lastX;
            double dz = pos.Z - _lastZ;
            double actualDisplacement = Math.Sqrt(dx * dx + dz * dz);
            bool stalled = locallyIntegrated
                           && _expectedDisplacement > 0.01
                           && actualDisplacement < _expectedDisplacement * StallDisplacementRatio;
            _stallTicks = stalled ? _stallTicks + 1 : 0;
            _lastX = pos.X;
            _lastZ = pos.Z;
        }

        float rawThrottle = 0f;
        float rawSteer = 0f;
        bool engineOn = EngineOn;
        if (controlled && controls != null && engineOn)
        {
            bool forward = controls.Forward;
            bool backward = controls.Backward;
            rawThrottle = forward == backward ? 0f : forward ? 1f : -1f;
        }
        // Steering needs power too — a dead boiler (no heat, no steam) leaves
        // the wheel locked even though a driver sits behind it.
        bool powered = engineOn && _maxSpeedFactor > 0.02f;
        if (controlled && controls != null && powered)
        {
            bool left = controls.Left;
            bool right = controls.Right;
            rawSteer = left == right ? 0f : left ? 1f : -1f;
        }

        _throttleInput = controlled && engineOn ? rawThrottle : 0f;
        _steerInput = controlled ? rawSteer : 0f;

        bool waterBlocked = _params.WaterOnly && !entity.FeetInLiquid;
        bool wantsMove = _throttleInput != 0f;

        // --- Longitudinal speed -------------------------------------------------
        if (controlled && wantsMove && !waterBlocked)
        {
            float throttle = _throttleInput < 0f ? _throttleInput * _params.ReverseFactor : _throttleInput;
            float target = throttle * _params.MaxSpeed * _maxSpeedFactor;

            bool braking = MathF.Abs(target) < MathF.Abs(_currentSpeed)
                           || (_currentSpeed != 0f && MathF.Sign(target) != MathF.Sign(_currentSpeed));
            float k = braking ? _params.BrakeK : _params.AccelK;
            _currentSpeed += (target - _currentSpeed) * GameMath.Clamp(k * dt, 0f, 1f);
        }
        else
        {
            _currentSpeed *= MathF.Pow(_params.CoastDrag, dt);
            if (MathF.Abs(_currentSpeed) < StandstillSpeed) _currentSpeed = 0f;
        }

        if (!float.IsFinite(_currentSpeed)) _currentSpeed = 0f;
        float speedCap = MathF.Max(0f, _params.MaxSpeed * _maxSpeedFactor);
        _currentSpeed = GameMath.Clamp(_currentSpeed, -speedCap * _params.ReverseFactor, speedCap);

        // --- Yaw rate ------------------------------------------------------------
        if (controlled)
        {
            float turnCurve = _params.TurnAtStandstill
                ? 1f
                : _params.MaxSpeed > 0.0001f
                    ? GameMath.Clamp(MathF.Abs(_currentSpeed) / _params.MaxSpeed * 3f, 0f, 1f)
                    : 0f;
            // Speed-sensitive steering: the wheel goes numb the faster the
            // vehicle moves (SteerFalloff = fraction still available at full
            // speed), and hard steering at speed bleeds drive speed.
            float speedFrac = _params.MaxSpeed > 0.0001f
                ? GameMath.Clamp(MathF.Abs(_currentSpeed) / _params.MaxSpeed, 0f, 1f)
                : 0f;
            float steerFactor = 1f + (_params.SteerFalloff - 1f) * speedFrac;
            float yawTarget = _steerInput * _params.TurnSpeed * turnCurve * steerFactor;
            _yawRate += (yawTarget - _yawRate) * GameMath.Clamp(_params.SteerK * dt, 0f, 1f);
            if (_params.SteerDragK > 0f && speedFrac > 0f)
            {
                _currentSpeed *= MathF.Pow(
                    1f - _params.SteerDragK * MathF.Abs(_steerInput) * speedFrac, dt);
            }
        }
        else
        {
            _yawRate = 0f;
        }

        if (!float.IsFinite(_yawRate)) _yawRate = 0f;

        // --- Apply yaw ------------------------------------------------------------
        // With a multibox physics behavior installed, rotating the collision boxes into
        // terrain would wedge the entity - AdjustCollisionBoxesToYaw pre-rotates the boxes
        // and reports whether the new orientation is free (it may push the entity out
        // instead). Without it the single axis-aligned collision box is yaw-invariant.
        float yawDelta = _yawRate * (1f - _params.Understeer) * dt;
        if (_multiBoxPhysics != null)
        {
            // A parked vehicle's boxes are already aligned — skip the per-tick
            // rotate + terrain collision test unless the yaw actually changes.
            if (yawDelta != 0f || pos.Yaw != _boxesAlignedYaw)
            {
                float newYaw = pos.Yaw + yawDelta;
                if (_multiBoxPhysics.AdjustCollisionBoxesToYaw(dt, true, newYaw))
                {
                    pos.Yaw = newYaw;
                }
                else if (yawDelta != 0f)
                {
                    // Rotation was rejected; re-align the boxes with the unchanged yaw.
                    _multiBoxPhysics.AdjustCollisionBoxesToYaw(dt, true, pos.Yaw);
                }
                _boxesAlignedYaw = pos.Yaw;
            }
        }
        else
        {
            pos.Yaw += yawDelta;
        }

        // --- Horizontal motion ------------------------------------------------------
        // While pressed against terrain, bleed the drive speed: otherwise full-throttle
        // motion keeps re-penetrating the collision boxes every tick and the multibox
        // physics push-out bounces the entity back and forth (constant jitter at walls).
        // Skipped while a step-up arc is in flight - pressing the face mid-air is what
        // carries the vehicle over the lip.
        _climbTimer = Math.Max(0f, _climbTimer - dt);
        bool climbing = _climbTimer > 0f && pos.Motion.Y > 0.005f;
        if (entity.CollidedHorizontally && !climbing)
        {
            _currentSpeed *= MathF.Pow(0.001f, dt);
            if (MathF.Abs(_currentSpeed) < StandstillSpeed) _currentSpeed = 0f;
        }

        // EntityPos.Motion is expressed in blocks per 1/60 s (physics integrates
        // pos += motion * dt * 60). VS entity facing convention is +Z at yaw 0 —
        // EntityBoat moves along -GetViewVector() = (sin(yaw), cos(yaw)).
        float speedPerFrame = _currentSpeed / 60f;
        var motion = pos.Motion;
        motion.X = GameMath.Sin(pos.Yaw) * speedPerFrame;
        motion.Z = GameMath.Cos(pos.Yaw) * speedPerFrame;

        // --- Blocked state ----------------------------------------------------------
        bool horizontallyBlocked = _stallTicks >= StallTicksToBlock
                                   || (entity.CollidedHorizontally && wantsMove);
        _isBlocked = wantsMove && (waterBlocked || horizontallyBlocked);

        // --- Step-up assist ---------------------------------------------------------
        // When forward motion is stalled, give the entity an upward impulse sized to
        // arc just over the obstacle instead of teleporting - a flat pos.Y bump pops
        // the whole vehicle (and the driver's camera) every cooldown and reads as
        // judder, while the impulse climb is one smooth hop.
        _stepUpCooldown -= dt;
        if (horizontallyBlocked && wantsMove && _params.StepUpMax > 0f && _stepUpCooldown <= 0f
            && (entity.OnGround || entity.CollidedVertically || entity.FeetInLiquid))
        {
            double lift = FindStepLift(pos);
            if (lift > 0)
            {
                // Motion units: gravity is GravityPerSecond/60 per frame^2, so the
                // impulse reaching height h is sqrt(2*g*h); a small margin clears
                // the lip of the obstacle.
                double impulse = Math.Sqrt(2.0 * GlobalConstants.GravityPerSecond / 60.0 * (lift + 0.12));
                if (pos.Motion.Y < impulse) pos.Motion.Y = impulse;
                _stepUpCooldown = StepUpCooldownSeconds;
                _climbTimer = 0.6f;
            }
        }

        // --- Direct integration fallback ---------------------------------------------
        // Vanilla passive physics skips server-side integration while a controllable seat
        // is occupied, and only a player controller has a client that simulates the entity
        // instead. For any other controlling passenger nothing would move the entity, so
        // integrate the horizontal motion with a simple terrain collision pass.
        if (server && controlled && controller is not EntityPlayer && _currentSpeed != 0f)
        {
            float dtFactor = dt * 60f;
            double nextX = pos.X + motion.X * dtFactor;
            double nextY = pos.Y + motion.Y * dtFactor;
            double nextZ = pos.Z + motion.Z * dtFactor;

            entity.World.CollisionTester.ApplyTerrainCollision(entity, pos, dtFactor, ref _integrationResult, 0f, 0f);
            pos.SetPos(_integrationResult);

            if ((nextX < _integrationResult.X && motion.X < 0) || (nextX > _integrationResult.X && motion.X > 0)) motion.X = 0;
            if ((nextY < _integrationResult.Y && motion.Y < 0) || (nextY > _integrationResult.Y && motion.Y > 0)) motion.Y = 0;
            if ((nextZ < _integrationResult.Z && motion.Z < 0) || (nextZ > _integrationResult.Z && motion.Z > 0)) motion.Z = 0;
        }

        _expectedDisplacement = MathF.Abs(_currentSpeed) * dt;
    }

    /// <summary>
    /// Probes the smallest lift in the 0..<see cref="DrivableParams.StepUpMax" /> range
    /// at which the entity's collision box no longer overlaps terrain, in 0.25 block
    /// increments. Returns 0 when no lift within the budget clears the box.
    /// The probe runs a hair ahead in the drive direction and requires the box to
    /// collide there at zero lift first — a glancing side contact (box edge brushing
    /// a slope corner) must not trigger the hop, only a real obstacle in the way.
    /// </summary>
    /// <param name="pos">Current entity position.</param>
    private double FindStepLift(EntityPos pos)
    {
        var tester = entity.World.CollisionTester;
        var accessor = entity.World.BlockAccessor;
        var box = entity.CollisionBox;
        double ahead = 0.2 * MathF.Sign(_currentSpeed == 0f ? 1f : _currentSpeed);
        double dirX = GameMath.Sin(pos.Yaw) * ahead;
        double dirZ = GameMath.Cos(pos.Yaw) * ahead;
        var frontPos = new Vec3d(pos.X + dirX, pos.InternalY, pos.Z + dirZ);
        if (!tester.IsColliding(accessor, box, frontPos, false)) return 0;
        for (double lift = 0.25; lift <= _params.StepUpMax + 0.001; lift += 0.25)
        {
            var testPos = new Vec3d(frontPos.X, frontPos.Y + lift, frontPos.Z);
            if (!tester.IsColliding(accessor, box, testPos, false)) return lift;
        }
        return 0;
    }

    /// <summary>
    /// Whether <paramref name="controller" /> is the entity of the local client player.
    /// </summary>
    private bool IsLocalPlayer(Entity controller)
    {
        return entity.Api is ICoreClientAPI capi
               && capi.World.Player?.Entity?.EntityId == controller.EntityId;
    }

    /// <summary>
    /// Writes <see cref="CurrentSpeed" /> and <see cref="YawRate" /> to the watched
    /// attributes at most every <see cref="SyncIntervalSeconds" /> seconds so observing
    /// clients and savegames see the current drive state.
    /// </summary>
    private void SyncAttributes(float dt)
    {
        _syncAccum += dt;
        if (_syncAccum < SyncIntervalSeconds) return;
        _syncAccum = 0f;

        var wa = entity.WatchedAttributes;
        wa.SetFloat(SpeedAttributeKey, _currentSpeed);
        wa.SetFloat(YawAttributeKey, _yawRate);
    }
}
