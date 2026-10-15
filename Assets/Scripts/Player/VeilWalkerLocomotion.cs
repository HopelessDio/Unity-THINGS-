using UnityEngine;

namespace Survivor.Player
{
    /// <summary>Visual-only, in-place locomotion for the sword-ready generic rig.</summary>
    [DisallowMultipleComponent]
    public sealed class VeilWalkerLocomotion : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float referenceSpeed = 6f;
        [SerializeField, Min(.1f)] private float cyclesPerSecond = 1.7f;
        [SerializeField, Range(0f, .4f)] private float strideReach = .22f;
        [SerializeField, Range(.2f, 1f)] private float backwardStride = .8f;
        [SerializeField, Range(.2f, 1f)] private float sidewaysStride = .5f;
        [SerializeField, Range(0f, .25f)] private float footLift = .13f;
        [SerializeField, Range(0f, .1f)] private float bodyBob = .025f;
        [SerializeField, Min(.1f)] private float blendSpeed = 10f;
        [Header("Running (default movement)")]
        [SerializeField] private bool runByDefault = true;
        [SerializeField, Range(.1f, .6f)] private float runReach = .55f;
        [SerializeField, Range(.1f, .4f)] private float runLift = .19f;
        [SerializeField, Range(.2f, .45f)] private float runGroundFraction = .43f;
        [SerializeField, Range(0f, 20f)] private float runLean = 10f;
        [Header("Combat-run upper body")]
        [SerializeField, Range(0f, 15f)] private float shoulderTwist = 2.5f;
        [SerializeField, Range(0f, 8f)] private float shoulderRock = 0f;
        [SerializeField, Range(0f, .2f)] private float runCrouch = .18f;
        [SerializeField, Range(-.05f, .1f)] private float runStanceWidth = -.039f;
        [Header("Braced two-handed running guard")]
        [SerializeField, Range(0f, 50f)] private float swordCarryPitch = 35f;
        [SerializeField, Range(0f, .1f)] private float swordCarryForward = .05f;
        [Header("Dash animation (visual only)")]
        [SerializeField, Min(.01f)] private float dashBlendIn = .035f;
        [SerializeField, Min(.01f)] private float dashBlendOut = .11f;
        [SerializeField, Range(0f, 40f)] private float dashLean = 26f;
        [SerializeField, Range(0f, .35f)] private float dashCrouch = .23f;

        private Rigidbody body;
        private PlayerAim facing;
        private PlayerMovement movement;
        private GreatSwordLocomotion importedMovement;
        private Quaternion previousFacing;
        private Vector3 previousInputHeading;
        private Transform player;
        private Transform hips;
        private Vector3 hipsRest;
        private Quaternion hipsRotation;
        private Transform spine, chest, head;
        private Quaternion spineRest, chestRest, headRest;
        private Leg left;
        private Leg right;
        private Leg leftArm, rightArm;
        private PlayerWeaponEquipment equipment;
        private Transform[] cape;
        private Quaternion[] capeRest;
        private Vector3 travelDirection;
        private float weight;
        private float phase;
        private float runWeight;
        private Vector3 previousPosition;
        private bool ready;
        private float dashWeight, dashPoseProgress, dashLeadSide;
        private bool wasDashing;
        private Transform[] dashBones;
        private Quaternion[] beforeDashRotations;
        private Vector3[] beforeDashPositions;
        private Vector3 supportOffset;
        private Quaternion supportOrientation;
        private Vector3 rightGripOffset;
        private Quaternion rightGripOrientation;
        private bool swordStartCaptured;
        private Vector3 swordStartPalm, swordStartRightPole, swordStartLeftPole;
        private Quaternion swordStartSocket, swordStartChest;

        private sealed class Leg
        {
            public Transform upper, lower, foot;
            public Quaternion upperRest, lowerRest, footRest;
            public Vector3 footPoint;
            public Quaternion footOrientation;
            public bool planted;
            public Vector3 groundPoint;
            public float outwardSign;
        }

        public bool IsReady => ready;
        public bool RunByDefault { get => runByDefault; set => runByDefault = value; }

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (ready) return;
            body = GetComponentInParent<Rigidbody>();
            if (body == null) return;
            player = body.transform;
            facing = body.GetComponent<PlayerAim>();
            movement = body.GetComponent<PlayerMovement>();
            importedMovement=GetComponent<GreatSwordLocomotion>();
            previousFacing = player.rotation;
            previousInputHeading = player.forward;
            hips = FindBone("Hips");
            left = CaptureLeg("Left");
            right = CaptureLeg("Right");
            leftArm = CaptureChain("LeftUpperArm", "LeftLowerArm", "LeftHand");
            rightArm = CaptureChain("RightUpperArm", "RightLowerArm", "RightHand");
            equipment = GetComponent<PlayerWeaponEquipment>();
            cape = new[] { FindBone("CapeUpper"), FindBone("CapeMiddle"), FindBone("CapeLower") };
            capeRest = new Quaternion[cape.Length];
            if (hips == null || left == null || right == null) return;
            // Use real resting sides: the imported Left/Right labels are
            // mirrored relative to gameplay X after the facing conversion.
            left.outwardSign = Mathf.Sign(Vector3.Dot(left.foot.position - right.foot.position, player.right));
            right.outwardSign = -left.outwardSign;
            hipsRest = hips.localPosition;
            hipsRotation = hips.localRotation;
            spine = FindBone("Spine");
            chest = FindBone("Chest");
            head = FindBone("Head");
            if (spine != null) spineRest = spine.localRotation;
            if (chest != null) chestRest = chest.localRotation;
            if (head != null) headRest = head.localRotation;
            for (int i = 0; i < cape.Length; i++)
                if (cape[i] != null) capeRest[i] = cape[i].localRotation;
            travelDirection = player.forward;
            previousPosition = player.position;
            dashBones = GetComponentsInChildren<Transform>(true);
            beforeDashRotations = new Quaternion[dashBones.Length];
            beforeDashPositions = new Vector3[dashBones.Length];
            if (equipment != null && equipment.SwordSocket != null && equipment.SwordOffhandGrip != null && leftArm != null)
            {
                supportOffset = Quaternion.Inverse(equipment.SwordSocket.rotation) *
                    (leftArm.foot.position - (importedMovement!=null && equipment.GunSocket!=null ? equipment.GunSocket.position : equipment.SwordOffhandGrip.position));
                supportOrientation = Quaternion.Inverse(equipment.SwordSocket.rotation) * leftArm.foot.rotation;
                rightGripOffset = Quaternion.Inverse(equipment.SwordSocket.rotation) *
                    (rightArm.foot.position - equipment.SwordSocket.position);
                rightGripOrientation = Quaternion.Inverse(equipment.SwordSocket.rotation) * rightArm.foot.rotation;
                if(importedMovement!=null)
                {
                    Quaternion socket=equipment.SwordSocket.rotation;
                    SolveLeg(leftArm,equipment.SwordOffhandGrip.position+socket*supportOffset,
                        leftArm.lower.position-leftArm.upper.position);
                    leftArm.foot.rotation=socket*supportOrientation;
                    leftArm.upperRest=leftArm.upper.localRotation;
                    leftArm.lowerRest=leftArm.lower.localRotation;
                    leftArm.footRest=leftArm.foot.localRotation;
                }
            }
            ready = true;
        }

        private Transform FindBone(string boneName)
        {
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
                if (bone.name == boneName) return bone;
            string alias=boneName.Replace("UpperArm","Arm").Replace("LowerArm","ForeArm").Replace("UpperLeg","UpLeg").Replace("LowerLeg","Leg");
            if(alias=="Chest") alias="Spine2";
            foreach(Transform bone in GetComponentsInChildren<Transform>(true))
                if(bone.name=="mixamorig:"+alias) return bone;
            return null;
        }

        private Leg CaptureLeg(string side)
        {
            return CaptureChain(side + "UpperLeg", side + "LowerLeg", side + "Foot");
        }

        private Leg CaptureChain(string upperName, string lowerName, string endName)
        {
            Transform upper = FindBone(upperName);
            Transform lower = FindBone(lowerName);
            Transform foot = FindBone(endName);
            if (upper == null || lower == null || foot == null) return null;
            return new Leg
            {
                upper = upper, lower = lower, foot = foot,
                upperRest = upper.localRotation, lowerRest = lower.localRotation,
                footRest = foot.localRotation,
                footPoint = transform.InverseTransformPoint(foot.position),
                footOrientation = Quaternion.Inverse(transform.rotation) * foot.rotation
            };
        }

        private void LateUpdate()
        {
            if (!ready) Initialize();
            if (!ready) return;
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            float dt = Time.deltaTime;
            Vector3 displacement = player.position - previousPosition;
            previousPosition = player.position;
            displacement.y = 0f;
            weight = Mathf.MoveTowards(weight, speed > .08f ? 1f : 0f, blendSpeed * dt);
            runWeight = Mathf.MoveTowards(runWeight, runByDefault ? 1f : 0f, blendSpeed * dt);
            bool keyboardFacing = facing != null && facing.enabled && !facing.UsesMouseAim && movement != null && movement.enabled;
            bool canPlantFeet = !keyboardFacing || PrepareKeyboardRun();
            bool dashing = movement != null && movement.enabled && movement.IsDashing;
            if (dashing && !wasDashing)
            {
                dashLeadSide = Mathf.Cos(phase * Mathf.PI * 2f) >= 0f ? left.outwardSign : right.outwardSign;
                left.planted = right.planted = false;
            }
            if (dashing) dashPoseProgress = movement.DashProgress;
            dashWeight = Mathf.MoveTowards(dashWeight, dashing ? 1f : 0f,
                dt / (dashing ? dashBlendIn : dashBlendOut));
            wasDashing = dashing;
            if(importedMovement!=null && importedMovement.IsReady)
            {
                ApplyStandingGuard(importedMovement.StandingWeight);
                if(dashWeight>.0001f) ApplyDashOverlay(Mathf.SmoothStep(0,1,dashWeight),dashPoseProgress,dashLeadSide);
                else ApplySupportingGrip();
                return;
            }
            if (speed > .08f && !dashing)
            {
                // Smooth yaw only. A 180-degree reversal must not interpolate
                // through a vertical direction or make the feet stop stepping.
                float heading = Mathf.Atan2(travelDirection.x, travelDirection.z) * Mathf.Rad2Deg;
                float targetHeading = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                heading = Mathf.LerpAngle(heading, targetHeading, 1f - Mathf.Exp(-blendSpeed * dt));
                travelDirection = keyboardFacing ? player.forward
                    : Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                float walkAdvance = dt * cyclesPerSecond * Mathf.Clamp(speed / referenceSpeed, .35f, 1.35f);
                // Distance drives the run. During stance, relative foot travel
                // is exactly opposite the player's travel, not an arbitrary sine.
                float reach = RunStride(travelDirection).magnitude;
                float distance = Mathf.Min(displacement.magnitude, referenceSpeed * 1.5f * dt);
                float runAdvance = distance * runGroundFraction / Mathf.Max(.02f, 2f * reach);
                phase = Mathf.Repeat(phase + Mathf.Lerp(walkAdvance, runAdvance, runWeight), 1f);
            }
            ApplyPose(phase, weight, keyboardFacing ? player.forward : travelDirection, runWeight,
                canPlantFeet && dashWeight < .001f);
            if (dashWeight > .0001f)
                ApplyDashOverlay(Mathf.SmoothStep(0f, 1f, dashWeight), dashPoseProgress, dashLeadSide);
        }

        public void ApplyStandingGuard(float amount)
        {
            if(!ready || amount<.0001f || rightArm==null || leftArm==null || equipment==null || equipment.SwordSocket==null) return;
            Vector3 shoulders=(leftArm.upper.position+rightArm.upper.position)*.5f;
            Vector3 palm=shoulders-Vector3.up*.3f+player.forward*.32f;
            Quaternion socket=Quaternion.Slerp(equipment.SwordSocket.rotation,
                player.rotation*Quaternion.Euler(-12,0,0),Mathf.Clamp01(amount));
            palm=Vector3.Lerp(equipment.SwordSocket.position,palm,Mathf.Clamp01(amount));
            SolveLeg(rightArm,palm+socket*rightGripOffset,player.right-player.forward*.3f-Vector3.up*.2f);
            rightArm.foot.rotation=socket*rightGripOrientation;
            ApplySupportingGrip();
        }

        public void ApplySupportingGrip()
        {
            if(equipment==null || equipment.SwordSocket==null || equipment.SwordOffhandGrip==null || leftArm==null) return;
            Quaternion socket=equipment.SwordSocket.rotation;
            SolveLeg(leftArm,equipment.SwordOffhandGrip.position+socket*supportOffset,
                leftArm.lower.position-leftArm.upper.position);
            leftArm.foot.rotation=socket*supportOrientation;
        }

        private bool PrepareKeyboardRun()
        {
            Vector3 inputHeading = movement.FacingDirection;
            bool changedInput = inputHeading.sqrMagnitude > .001f &&
                Vector3.Dot(inputHeading.normalized, previousInputHeading) < .99f;
            // The old stance anchors belong to the old heading. Never drag a
            // planted ankle across the body during an interpolated keyboard turn.
            bool visualTurning = Quaternion.Angle(previousFacing, player.rotation) > .5f;
            bool stillTurning = inputHeading.sqrMagnitude > .001f &&
                Quaternion.Angle(body.rotation, Quaternion.LookRotation(inputHeading.normalized, Vector3.up)) > 2f;
            if (changedInput || visualTurning || stillTurning)
                left.planted = right.planted = false;
            if (inputHeading.sqrMagnitude > .001f) previousInputHeading = inputHeading.normalized;
            previousFacing = player.rotation;
            // Use continuous local foot paths throughout the turn, then resume
            // world-space planting. Replanting every turn frame creates popping.
            return !changedInput && !visualTurning && !stillTurning;
        }

        public void SampleKeyboardRun(float cycle, float amount, bool lockFeet = true)
        {
            if (!ready) return;
            bool canPlant = PrepareKeyboardRun();
            ApplyPose(cycle, amount, player.forward, 1f, lockFeet && canPlant);
        }

        /// <summary>Deterministic pose sampling also used by editor verification.</summary>
        public void SamplePose(float cycle, float amount, Vector3 worldDirection)
        {
            ApplyPose(cycle, amount, worldDirection, 0f, false);
        }

        public void SampleRunPose(float cycle, float amount, Vector3 worldDirection, bool lockFeet = false)
        {
            ApplyPose(cycle, amount, worldDirection, 1f, lockFeet);
        }

        public void SampleDashPose(float cycle, float dashAmount, float progress)
        {
            if (!ready) return;
            ApplyPose(cycle, 1f, player.forward, 1f, false);
            float lead = Mathf.Cos(cycle * Mathf.PI * 2f) >= 0f ? left.outwardSign : right.outwardSign;
            ApplyDashOverlay(Mathf.Clamp01(dashAmount), Mathf.Clamp01(progress), lead);
        }

        /// <summary>Upper-body overlay; locomotion and dash remain independent.</summary>
        public void CaptureSwordAttackStart()
        {
            if (!ready || equipment == null || equipment.SwordSocket == null) return;
            swordStartPalm = player.InverseTransformPoint(equipment.SwordSocket.position);
            swordStartSocket = Quaternion.Inverse(player.rotation) * equipment.SwordSocket.rotation;
            swordStartChest = chest.localRotation;
            swordStartRightPole = player.InverseTransformDirection(rightArm.lower.position-rightArm.upper.position);
            swordStartLeftPole = player.InverseTransformDirection(leftArm.lower.position-leftArm.upper.position);
            swordStartCaptured = true;
        }

        public void ClearSwordAttackStart() => swordStartCaptured = false;

        public void SampleSwordAttack(int strike, float progress, bool holdFollowThrough = false)
        {
            if (!ready || rightArm == null || leftArm == null || equipment == null || equipment.SwordSocket == null) return;
            float windup = Survivor.Combat.PlayerSwordAttack.WindupEnd(strike);
            float end = Survivor.Combat.PlayerSwordAttack.SwingEnd(strike);
            if (holdFollowThrough && progress > end) progress = end;
            float sweep = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(windup,end,progress));
            float strength = progress < windup ? Mathf.SmoothStep(0f,1f,progress/windup)
                : progress > end ? 1f-Mathf.SmoothStep(0f,1f,(progress-end)/(1f-end)) : 1f;
            bool capturedWindup = swordStartCaptured && progress <= windup;
            if (strength < .0001f && !capturedWindup) return;
            Vector3 basePalm = capturedWindup ? player.TransformPoint(swordStartPalm) : equipment.SwordSocket.position;
            Quaternion baseSocket = capturedWindup ? player.rotation*swordStartSocket : equipment.SwordSocket.rotation;
            // Keep the old bend plane at entry. Switching elbow poles abruptly
            // can flip the forearm even when the sword itself follows a curve.
            Vector3 rightPole = capturedWindup ? player.TransformDirection(swordStartRightPole)
                : rightArm.lower.position-rightArm.upper.position;
            Vector3 leftPole = capturedWindup ? player.TransformDirection(swordStartLeftPole)
                : leftArm.lower.position-leftArm.upper.position;
            if (capturedWindup)
            {
                rightPole=Vector3.Slerp(rightPole.normalized,
                    (rightArm.lower.position-rightArm.upper.position).normalized,strength);
                leftPole=Vector3.Slerp(leftPole.normalized,
                    (leftArm.lower.position-leftArm.upper.position).normalized,strength);
            }
            float twist = strike == 2 ? 0f : Mathf.Lerp(strike == 0 ? -25f : 25f, strike == 0 ? 25f : -25f, sweep);
            if (chest != null)
            {
                Quaternion goalChest = Quaternion.AngleAxis(twist, Vector3.up)*chest.rotation;
                if (strike == 2) goalChest=Quaternion.AngleAxis(Mathf.Lerp(-4f,10f,sweep),player.right)*goalChest;
                Quaternion sourceChest = capturedWindup ? swordStartChest : chest.localRotation;
                chest.localRotation=Quaternion.Slerp(sourceChest,Quaternion.Inverse(chest.parent.rotation)*goalChest,strength);
            }
            Quaternion windRotation, endRotation;
            Vector3 windHand, endHand, controlHand;
            if (strike == 0)
            {
                windRotation=Quaternion.Euler(12f,-65f,-15f); endRotation=Quaternion.Euler(12f,65f,15f);
                windHand = new Vector3(-.12f,-.1f,.22f); endHand = new Vector3(.12f,-.14f,.23f);
                controlHand=new Vector3(0f,-.02f,.35f);
            }
            else if (strike == 1)
            {
                windRotation=Quaternion.Euler(12f,65f,15f); endRotation=Quaternion.Euler(12f,-65f,-15f);
                windHand = new Vector3(.12f,-.14f,.23f); endHand = new Vector3(-.12f,-.1f,.22f);
                controlHand=new Vector3(0f,-.02f,.35f);
            }
            else
            {
                windRotation=Quaternion.Euler(-110f,0f,0f); endRotation=Quaternion.Euler(65f,0f,0f);
                windHand = new Vector3(0f,.1f,.18f); endHand = new Vector3(0f,-.23f,.25f);
                controlHand=new Vector3(0f,.01f,.34f);
            }
            Vector3 shoulderCentre = (leftArm.upper.position + rightArm.upper.position)*.5f;
            float remaining=1f-sweep;
            Vector3 localHand=remaining*remaining*windHand+2f*remaining*sweep*controlHand+sweep*sweep*endHand;
            Vector3 palm = shoulderCentre + player.right*localHand.x + Vector3.up*localHand.y + player.forward*localHand.z;
            Quaternion desiredSocket = player.rotation * Quaternion.Slerp(windRotation,endRotation,sweep);
            desiredSocket = Quaternion.Slerp(baseSocket,desiredSocket,strength);
            palm = Vector3.Lerp(basePalm,palm,strength);
            SolveLeg(rightArm,palm+desiredSocket*rightGripOffset,rightPole);
            rightArm.foot.rotation = desiredSocket * rightGripOrientation;
            Quaternion actualSocket = equipment.SwordSocket.rotation;
            SolveLeg(leftArm,equipment.SwordOffhandGrip.position+actualSocket*supportOffset,leftPole);
            leftArm.foot.rotation = actualSocket * supportOrientation;
        }

        private void ApplyDashOverlay(float amount, float progress, float leadSide)
        {
            if (amount < .0001f) return;
            for (int i = 0; i < dashBones.Length; i++)
            {
                beforeDashRotations[i] = dashBones[i].localRotation;
                beforeDashPositions[i] = dashBones[i].localPosition;
            }
            ResetPose();
            left.planted = right.planted = false;
            float drive = Mathf.Sin(progress * Mathf.PI);
            hips.position -= Vector3.up * (dashCrouch + drive * .025f);
            hips.rotation = Quaternion.AngleAxis(dashLean, player.right) * hips.rotation;
            RotateBone(spine, spineRest, player.right, 3f);
            RotateBone(head, headRest, player.right, -6f);
            // Both hands remain braced on the same separate sword.
            PoseSwordGuard(1f);
            PoseDashLeg(left, left.outwardSign == leadSide, drive);
            PoseDashLeg(right, right.outwardSign == leadSide, drive);
            for (int i = 0; i < cape.Length; i++)
            {
                if (cape[i] == null) continue;
                RotateBone(cape[i], capeRest[i], player.right, 18f + i * 7f + drive * 3f);
            }
            for (int i = 0; i < dashBones.Length; i++)
            {
                dashBones[i].localPosition = Vector3.Lerp(beforeDashPositions[i], dashBones[i].localPosition, amount);
                dashBones[i].localRotation = Quaternion.Slerp(beforeDashRotations[i], dashBones[i].localRotation, amount);
            }
            // Independent arm blending can loosen the supporting grip mid-fade.
            // Re-solve that hand to the sword socket after blending the bones.
            if (equipment != null && equipment.SwordOffhandGrip != null && equipment.SwordSocket != null && leftArm != null)
            {
                Quaternion socketRotation = equipment.SwordSocket.rotation;
                Vector3 target = equipment.SwordOffhandGrip.position + socketRotation * supportOffset;
                SolveLeg(leftArm, target, -player.right - player.forward * .3f - Vector3.up * .2f);
                leftArm.foot.rotation = socketRotation * supportOrientation;
            }
        }

        private void PoseDashLeg(Leg leg, bool leading, float drive)
        {
            Vector3 target = transform.TransformPoint(leg.footPoint);
            target += player.right * (leg.outwardSign * runStanceWidth);
            target += player.forward * (leading ? .32f + drive * .04f : -.34f - drive * .04f);
            target += Vector3.up * (leading ? .035f : .12f + drive * .035f);
            SolveLeg(leg, target, player.forward + player.right * (leg.outwardSign * .144f));
            leg.foot.rotation = Quaternion.AngleAxis(leading ? -5f : 15f, player.right)
                * transform.rotation * leg.footOrientation;
        }

        private void ApplyPose(float cycle, float amount, Vector3 worldDirection, float run, bool lockFeet)
        {
            if (!ready) return;
            ResetPose();
            float blend = Mathf.Clamp01(amount);
            if (blend < .0001f)
            {
                left.planted = right.planted = false;
                return;
            }
            Vector3 direction = Vector3.ProjectOnPlane(worldDirection, Vector3.up).normalized;
            if (direction.sqrMagnitude < .01f) direction = player.forward;
            float angle = cycle * Mathf.PI * 2f;
            // Move the pelvis in world units, not imported FBX bone units.
            hips.position += Vector3.up * ((Mathf.Cos(angle * 2f) - 1f) * bodyBob * Mathf.Lerp(1f, .6f, run) * blend);
            // Lower the pelvis for bent knees and room for the longer stride.
            hips.position += Vector3.up * ((-runCrouch + .018f * Mathf.Sin(angle * 2f)) * run * blend);
            // No lateral hip waggle: the pelvis stays centred over the stride.
            Vector3 leanAxis = hips.parent.InverseTransformDirection(Vector3.Cross(Vector3.up, direction));
            hips.localRotation = Quaternion.AngleAxis(runLean * run * blend, leanAxis) * hipsRotation;
            PoseUpperBody(angle, run * blend, direction);
            PoseSwordGuard(run * blend);
            PoseLeg(left, angle, blend, direction, run, lockFeet);
            PoseLeg(right, angle + Mathf.PI, blend, direction, run, lockFeet);
            // Arms move with the animated chest as one sword-ready assembly.
            for (int i = 0; i < cape.Length; i++)
            {
                if (cape[i] == null) continue;
                float flutter = Mathf.Sin(angle + i * .65f) * (2f + i);
                Vector3 axis = cape[i].parent.InverseTransformDirection(player.right);
                cape[i].localRotation = Quaternion.AngleAxis((4f + flutter) * (1f + .4f * run) * blend, axis) * capeRest[i];
            }
        }

        private void PoseSwordGuard(float strength)
        {
            if (strength < .0001f) return;
            if (leftArm == null || rightArm == null || equipment == null ||
                equipment.EquippedSword == null || equipment.SwordSocket == null) return;
            // Transform the complete two-hand grip as one rigid assembly. Solve
            // both arms to those targets rather than rotating the sword alone.
            Vector3 pivot = equipment.SwordSocket.position;
            Quaternion carry = Quaternion.AngleAxis(swordCarryPitch * strength, player.right);
            Vector3 shift = (player.forward * swordCarryForward - Vector3.up * .03f) * strength;
            Vector3 rightTarget = pivot + carry * (rightArm.foot.position - pivot) + shift;
            Vector3 leftTarget = pivot + carry * (leftArm.foot.position - pivot) + shift;
            Quaternion rightOrientation = carry * rightArm.foot.rotation;
            Quaternion leftOrientation = carry * leftArm.foot.rotation;
            // Elbows stay out from the ribs, in a braced guard rather than swaying.
            SolveLeg(rightArm, rightTarget, player.right - player.forward * .3f - Vector3.up * .2f);
            rightArm.foot.rotation = rightOrientation;
            SolveLeg(leftArm, leftTarget, -player.right - player.forward * .3f - Vector3.up * .2f);
            leftArm.foot.rotation = leftOrientation;
        }

        private void PoseUpperBody(float angle, float strength, Vector3 direction)
        {
            float pulse = Mathf.Sin(angle);
            Vector3 pitchAxis = Vector3.Cross(Vector3.up, direction);
            // Waist flex and alternating shoulder lead distribute the motion
            // through the torso instead of tilting the entire model like a plank.
            RotateBone(spine, spineRest, pitchAxis, (2f + .6f * Mathf.Cos(angle * 2f)) * strength);
            if (chest != null)
            {
                Quaternion twist = Quaternion.AngleAxis(pulse * shoulderTwist * strength,
                    chest.parent.InverseTransformDirection(Vector3.up));
                Quaternion rock = Quaternion.AngleAxis(-pulse * shoulderRock * strength,
                    chest.parent.InverseTransformDirection(player.forward));
                Quaternion drive = Quaternion.AngleAxis(Mathf.Cos(angle * 2f) * 1.5f * strength,
                    chest.parent.InverseTransformDirection(pitchAxis));
                chest.localRotation = drive * rock * twist * chestRest;
            }
            // Counter-motion keeps the mask focused on the aim direction while
            // the shoulders and sword have visible stride-driven movement.
            RotateBone(head, headRest, Vector3.up, -pulse * shoulderTwist * .4f * strength);
        }

        private static void RotateBone(Transform bone, Quaternion rest, Vector3 worldAxis, float degrees)
        {
            if (bone == null) return;
            bone.localRotation = Quaternion.AngleAxis(degrees,
                bone.parent.InverseTransformDirection(worldAxis)) * rest;
        }

        private Vector3 RunStride(Vector3 direction)
        {
            float forward = Vector3.Dot(direction, player.forward);
            float length = runReach * Mathf.Lerp(.8f, 1f, Mathf.Abs(forward));
            if (forward < 0f) length *= .9f;
            return direction * length;
        }

        private void PoseLeg(Leg leg, float angle, float blend, Vector3 direction, float run, bool lockFeet)
        {
            Vector3 rest = transform.TransformPoint(leg.footPoint);
            Vector3 target = rest;
            // Blend continuously in aim-relative space rather than snapping
            // between eight clips. Diagonal angles retain both components.
            float forward = Vector3.Dot(direction, player.forward);
            float sideways = Vector3.Dot(direction, player.right);
            Vector3 stride = player.forward * (forward * (forward < 0f ? backwardStride : 1f))
                + player.right * (sideways * sidewaysStride);
            target += stride * (Mathf.Cos(angle) * strideReach * blend);
            target += Vector3.up * (Mathf.Max(0f, Mathf.Sin(angle)) * footLift * blend);
            float cycle = Mathf.Repeat(angle / (2f * Mathf.PI), 1f);
            bool stance = cycle < runGroundFraction;
            float progress = stance ? cycle / runGroundFraction : (cycle - runGroundFraction) / (1f - runGroundFraction);
            Vector3 runStride = RunStride(direction);
            float travel = stance ? Mathf.Lerp(1f, -1f, progress) : Mathf.Lerp(-1f, 1f, Mathf.SmoothStep(0f, 1f, progress));
            float side = leg.outwardSign;
            Vector3 runTarget = rest + player.right * (side * runStanceWidth) + runStride * travel;
            if (!stance) runTarget += Vector3.up * (Mathf.Sin(progress * Mathf.PI) * runLift);
            bool shouldPlant = lockFeet && run > .98f && blend > .98f && stance;
            if (shouldPlant)
            {
                if (!leg.planted) leg.groundPoint = runTarget;
                leg.planted = true;
                runTarget = leg.groundPoint;
            }
            else leg.planted = false;
            target = Vector3.Lerp(target, Vector3.Lerp(rest, runTarget, blend), run);
            // Knees track slightly outward instead of collapsing toward the
            // centreline, while the original walk's knee direction is preserved.
            SolveLeg(leg, target, player.forward + player.right * (side * .144f * run));
            leg.foot.rotation = transform.rotation * leg.footOrientation;
            // Toe-down recovery makes the lifted foot read as a run, not a shuffle.
            // Flat mid-stance, then toe push-off; no frozen heel-first foot.
            float toe = stance
                ? Mathf.Lerp(0f, 8f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.8f, 1f, progress)))
                : Mathf.Lerp(8f, 0f, progress) - Mathf.Sin(progress * Mathf.PI) * 5f;
            leg.foot.rotation = Quaternion.AngleAxis(toe * run * blend, player.right) * leg.foot.rotation;
        }

        private static void SolveLeg(Leg leg, Vector3 target, Vector3 kneeDirection)
        {
            Vector3 origin = leg.upper.position;
            float upperLength = Vector3.Distance(origin, leg.lower.position);
            float lowerLength = Vector3.Distance(leg.lower.position, leg.foot.position);
            if (upperLength < .0001f || lowerLength < .0001f) return;
            Vector3 offset = target - origin;
            if (offset.sqrMagnitude < .000001f) return;
            Vector3 axis = offset.normalized;
            float distance = Mathf.Clamp(offset.magnitude,
                Mathf.Abs(upperLength - lowerLength) + .0001f, upperLength + lowerLength - .0001f);
            Vector3 bend = Vector3.ProjectOnPlane(kneeDirection, axis).normalized;
            if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(Vector3.right, axis).normalized;
            float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 knee = origin + axis * along + bend * height;
            leg.upper.rotation = Quaternion.FromToRotation(leg.lower.position - origin, knee - origin) * leg.upper.rotation;
            Vector3 ankle = origin + axis * distance;
            leg.lower.rotation = Quaternion.FromToRotation(leg.foot.position - leg.lower.position,
                ankle - leg.lower.position) * leg.lower.rotation;
        }

        public void ResetPose()
        {
            if (!ready) return;
            hips.localPosition = hipsRest;
            hips.localRotation = hipsRotation;
            if (spine != null) spine.localRotation = spineRest;
            if (chest != null) chest.localRotation = chestRest;
            if (head != null) head.localRotation = headRest;
            ResetLeg(left);
            ResetLeg(right);
            if (leftArm != null) ResetLeg(leftArm);
            if (rightArm != null) ResetLeg(rightArm);
            for (int i = 0; i < cape.Length; i++)
                if (cape[i] != null) cape[i].localRotation = capeRest[i];
        }

        private static void ResetLeg(Leg leg)
        {
            leg.upper.localRotation = leg.upperRest;
            leg.lower.localRotation = leg.lowerRest;
            leg.foot.localRotation = leg.footRest;
        }

        private void OnDisable()
        {
            dashWeight = 0f;
            wasDashing = false;
            ResetPose();
            if (left != null) left.planted = false;
            if (right != null) right.planted = false;
        }
    }
}
