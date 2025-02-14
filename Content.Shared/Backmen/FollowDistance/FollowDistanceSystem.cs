﻿using System.Numerics;
using Content.Shared.Backmen.CameraFollow.Components;
using Content.Shared.Backmen.CameraFollow.Events;
using Content.Shared.Backmen.FollowDistance.Components;
using Content.Shared.Camera;
using Content.Shared.Hands;
using Robust.Shared.Network;

namespace Content.Shared.Backmen.FollowDistance;
/// <summary>
/// System to set new max distance and back strength for <see cref="CameraFollowComponent"/>
/// </summary>
public sealed class FollowDistanceSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly Actions.SharedActionsSystem _actionsSystem = default!; // Stalker-Changes
    private EntityQuery<CameraRecoilComponent> _activeRecoil;
    private EntityQuery<EyeComponent> _activeEye;
    private EntityQuery<CameraFollowComponent> _activeCamera;

    public override void Initialize()
    {
        SubscribeLocalEvent<FollowDistanceComponent, HandSelectedEvent>(OnPickedUp);
        SubscribeLocalEvent<FollowDistanceComponent, HandDeselectedEvent>(OnDropped);
        SubscribeLocalEvent<CameraFollowComponent, ComponentRemove>(OnCameraFollowRemove);
        SubscribeLocalEvent<CameraFollowComponent, ComponentInit>(OnCameraFollowInit);

        SubscribeLocalEvent<CameraFollowComponent, GetEyeOffsetEvent>(OnCameraRecoilGetEyeOffset);

        SubscribeAllEvent<ChangeCamOffsetEvent>(OnChangeOffset);

        _activeRecoil = GetEntityQuery<CameraRecoilComponent>();
        _activeEye = GetEntityQuery<EyeComponent>();
        _activeCamera = GetEntityQuery<CameraFollowComponent>();
    }

    private void OnChangeOffset(ChangeCamOffsetEvent msg, EntitySessionEventArgs args)
    {
        var plr = args.SenderSession.AttachedEntity;
        if(plr == null || !_activeCamera.TryComp(plr, out var cameraFollowComponent))
            return;
        cameraFollowComponent.Offset = msg.Offset;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsServer)
            UpdateEyes(frameTime);
    }

    private void UpdateEyes(float frameTime)
    {
        var query = AllEntityQuery<CameraRecoilComponent, EyeComponent, CameraFollowComponent>();

        while (query.MoveNext(out var uid, out var recoil, out var eye, out var follow))
        {
            if(!follow.Enabled)
                continue;

            var offset = recoil.BaseOffset + recoil.CurrentKick + follow.Offset;
            if (Math.Abs(follow.Offset.X) > Math.Abs(follow.MaxDistance.X) ||
                Math.Abs(follow.Offset.Y) > Math.Abs(follow.MaxDistance.Y))
            {
                follow.Offset = Vector2.Lerp(offset, follow.MaxDistance, 0);
                Dirty(uid,follow);
            }
            _eye.SetOffset(uid, offset, eye);
        }
    }

    private void OnCameraRecoilGetEyeOffset(Entity<CameraFollowComponent> ent, ref GetEyeOffsetEvent arg)
    {
        if (!ent.Comp.Enabled || !_activeRecoil.TryComp(ent, out var recoil))
            return;

        arg.Offset = recoil.BaseOffset + recoil.CurrentKick + ent.Comp.Offset; // Stalker-Changes
    }

    private void OnCameraFollowInit(EntityUid uid, CameraFollowComponent component, ComponentInit args) // Stalker-Changes-Start
    {
        _actionsSystem.AddAction(uid, ref component.ActionEntity, component.Action);
    }

    private void OnCameraFollowRemove(EntityUid uid, CameraFollowComponent component, ComponentRemove args)
    {
        _actionsSystem.RemoveAction(uid, component.ActionEntity);
    } // Stalker-Changes-End

    private void OnPickedUp(EntityUid uid, FollowDistanceComponent followDistance, HandSelectedEvent args)
    {
        if (!_activeCamera.TryComp(args.User, out var camfollow) || !_activeEye.HasComp(args.User))
            return;

        camfollow.MaxDistance = followDistance.MaxDistance;
        camfollow.BackStrength = followDistance.BackStrength;
        //camfollow.Enabled = true;
        Dirty(args.User, camfollow);
    }

    private void OnDropped(EntityUid uid, FollowDistanceComponent followDistance, HandDeselectedEvent args)
    {
        if (!_activeCamera.TryComp(args.User, out var camfollow) || !_activeEye.HasComp(args.User))
            return;

        camfollow.MaxDistance = camfollow.DefaultMaxDistance;
        camfollow.BackStrength = camfollow.DefaultBackStrength;
        //camfollow.Enabled = false;
        Dirty(args.User, camfollow);
    }

}
