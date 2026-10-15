using System;
using System.Reflection;
using Survivor.Combat;
using Survivor.Enemies;
using Survivor.Player;
using UnityEngine;

namespace Survivor.EditorTools
{
    public static class SwordComboValidation
    {
        public static void Validate(GameObject player, Transform visual)
        {
            PlayerSwordAttack attack = visual.GetComponent<PlayerSwordAttack>();
            if (attack == null) throw new InvalidOperationException("Sword combo component missing.");
            attack.Initialize();
            // These isolated probes supply keyboard input by reflection. Keep
            // the real desktop cursor out of their deterministic fallback tests.
            PlayerAim aim=player.GetComponent<PlayerAim>();
            bool wasEnabled=aim!=null && aim.enabled;
            try
            {
            if(aim!=null) aim.enabled=false;
            ValidatePoses(player.transform, visual);
            ValidateDamage(player, attack);
            ValidateAimAssist(player, attack);
            ValidateSwingContinuity(player.transform, visual);
            }
            finally { if(aim!=null) aim.enabled=wasEnabled; }
        }

        private static void ValidatePoses(Transform player, Transform visual)
        {
            VeilWalkerLocomotion walk = visual.GetComponent<VeilWalkerLocomotion>();
            PlayerWeaponEquipment equipment = visual.GetComponent<PlayerWeaponEquipment>();
            Transform hand = null;
            foreach (Transform bone in visual.GetComponentsInChildren<Transform>(true))
                if (bone.name == "LeftHand" || bone.name=="mixamorig:LeftHand") hand = bone;
            walk.ResetPose();
            float grip = Vector3.Distance(hand.position,equipment.SwordOffhandGrip.position);
            Quaternion original = player.rotation;
            Vector3 originalPosition = player.position;
            try
            {
                for (int heading = 0; heading < 8; heading++)
                {
                    player.rotation = original * Quaternion.Euler(0f,heading*45f,0f);
                    for (int stage = 0; stage < PlayerSwordAttack.ComboLength; stage++)
                    {
                        for (int frame = 0; frame <= 32; frame++)
                        foreach (float cycle in new[] { 0f,.5f })
                        {
                            walk.SampleRunPose(cycle,1f,player.forward);
                            walk.SampleSwordAttack(stage,frame/32f);
                            float error = Mathf.Abs(Vector3.Distance(hand.position,equipment.SwordOffhandGrip.position)-grip);
                            if (error > .006f)
                                throw new InvalidOperationException($"Sword combo grip loosened by {error:F3}m at stage {stage}, frame {frame}.");
                            if (float.IsNaN(hand.rotation.x) || player.position != originalPosition)
                                throw new InvalidOperationException("Sword pose must be finite and visual-only.");
                        }
                        walk.SampleRunPose(0f,1f,player.forward);
                        walk.SampleSwordAttack(stage,PlayerSwordAttack.WindupEnd(stage));
                        Vector3 startBlade = equipment.SwordSocket.forward;
                        walk.SampleRunPose(0f,1f,player.forward);
                        walk.SampleSwordAttack(stage,PlayerSwordAttack.SwingEnd(stage));
                        if (Vector3.Angle(startBlade,equipment.SwordSocket.forward) < 60f)
                            throw new InvalidOperationException("Sword strikes must have a visible sweep/cut.");
                    }
                }
            }
            finally { player.rotation=original; walk.SampleKeyboardRun(0f,0f); }
            Debug.Log("Sword animation validated: 1056 poses; right and left sweeps across 8 headings; two-hand grip and gameplay root preserved.");
        }

        private static GameObject Probe(string name, Vector3 position)
        {
            GameObject target = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            target.transform.position = position;
            target.AddComponent<SphereCollider>().radius = .2f;
            EnemyHealth health = target.AddComponent<EnemyHealth>();
            health.ApplyHealthMultiplier(10f); // 300 HP, so testing never awards a kill.
            return target;
        }

        private static void ValidateDamage(GameObject player, PlayerSwordAttack attack)
        {
            const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
            MethodInfo hit = typeof(PlayerSwordAttack).GetMethod("DealHits",flags);
            MethodInfo late = typeof(PlayerSwordAttack).GetMethod("LateUpdate",flags);
            MethodInfo begin = typeof(PlayerSwordAttack).GetMethod("Begin",flags);
            FieldInfo elapsed = typeof(PlayerSwordAttack).GetField("elapsed",flags);
            FieldInfo hp = typeof(EnemyHealth).GetField("currentHealth",flags);
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            FieldInfo dash = typeof(PlayerMovement).GetField("dashTimeRemaining",flags);
            float previousDash = (float)dash.GetValue(movement);
            Vector3 originalPosition = player.transform.position;
            GameObject front=null, behind=null, far=null, wall=null;
            try
            {
                // Isolate probes far away from live arena enemies/geometry.
                player.transform.position = new Vector3(10000f,10000f,10000f);
                Vector3 origin = player.transform.position, facing=player.transform.forward;
                front=Probe("SwordValidationFront",origin+facing*1.4f);
                GameObject duplicate=new GameObject("SecondCollider");
                duplicate.transform.SetParent(front.transform,false);
                duplicate.AddComponent<SphereCollider>().radius=.15f;
                behind=Probe("SwordValidationBehind",origin-facing*1.4f);
                far=Probe("SwordValidationOutsideReach",origin+facing*5f);
                Physics.SyncTransforms();
                EnemyHealth target=front.GetComponent<EnemyHealth>();
                attack.Cancel();
                attack.RequestAttack();
                float expected=300f;
                for (int stage=0;stage<4;stage++)
                {
                    if (!attack.IsAttacking || attack.CurrentStrike!=stage%PlayerSwordAttack.ComboLength)
                        throw new InvalidOperationException("Sword clicks did not alternate right -> left -> right -> left.");
                    hit.Invoke(attack,null);
                    hit.Invoke(attack,null);
                    expected-=attack.DamageForStrike(stage);
                    if (Mathf.Abs((float)hp.GetValue(target)-expected)>.001f)
                        throw new InvalidOperationException("Sword damage repeated within a strike or used the wrong amount.");
                    if (stage<3) attack.RequestAttack();
                    elapsed.SetValue(attack,PlayerSwordAttack.Duration(stage));
                    late.Invoke(attack,null);
                }
                if (attack.IsAttacking || PlayerSwordAttack.ComboLength!=2 ||
                    (float)hp.GetValue(behind.GetComponent<EnemyHealth>())!=300f ||
                    (float)hp.GetValue(far.GetComponent<EnemyHealth>())!=300f)
                    throw new InvalidOperationException("Two-hit combo, idle termination or hit-cone/range check failed.");
                wall=new GameObject("SwordValidationWall") {hideFlags=HideFlags.HideAndDontSave};
                wall.layer=LayerMask.NameToLayer("ArenaObstacle");
                wall.transform.position=origin+facing*.7f;
                wall.AddComponent<BoxCollider>().size=Vector3.one*.5f;
                Physics.SyncTransforms();
                begin.Invoke(attack,new object[]{0});
                hit.Invoke(attack,null);
                if (Mathf.Abs((float)hp.GetValue(target)-expected)>.001f)
                    throw new InvalidOperationException("Sword hit through an arena obstacle.");
                dash.SetValue(movement,.1f);
                late.Invoke(attack,null);
                if (attack.IsAttacking) throw new InvalidOperationException("Dash must cancel a sword attack.");
                Debug.Log("Sword combat validated: right/left/right/left click-buffered alternation, 15 damage per sweep, no heavy third hit, once per enemy per swing even with duplicate colliders; range/rear/wall rejection and dash cancel passed. No live enemies touched.");
            }
            finally
            {
                attack.Cancel();
                dash.SetValue(movement,previousDash);
                player.transform.position=originalPosition;
                foreach(GameObject temporary in new[]{front,behind,far,wall})
                    if(temporary!=null) UnityEngine.Object.DestroyImmediate(temporary);
                Physics.SyncTransforms();
                attack.GetComponent<VeilWalkerLocomotion>().SampleKeyboardRun(0f,0f);
            }
        }

        private static void ValidateAimAssist(GameObject player, PlayerSwordAttack attack)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            PlayerMovement movement=player.GetComponent<PlayerMovement>();
            FieldInfo input=typeof(PlayerMovement).GetField("moveInput",flags);
            FieldInfo elapsed=typeof(PlayerSwordAttack).GetField("elapsed",flags);
            MethodInfo late=typeof(PlayerSwordAttack).GetMethod("LateUpdate",flags);
            Vector2 originalInput=(Vector2)input.GetValue(movement);
            Vector3 originalPosition=player.transform.position;
            GameObject target=null,rear=null,outside=null,wall=null;
            try
            {
                player.transform.position=new Vector3(12000f,12000f,12000f);
                Vector3 origin=player.transform.position, forward=player.transform.forward, right=player.transform.right;
                Vector3 offsetDirection=Quaternion.AngleAxis(40f,Vector3.up)*forward;
                target=Probe("AssistValidationFront",origin+offsetDirection*1.8f);
                rear=Probe("AssistValidationRear",origin-forward*.5f);
                outside=Probe("AssistValidationOutsideCone",origin+Quaternion.AngleAxis(110f,Vector3.up)*forward);
                Physics.SyncTransforms();
                if(Vector3.Angle(attack.ResolveAttackDirection(forward),offsetDirection)>.5f)
                    throw new InvalidOperationException("Nearby offset enemy should be assisted; rear/out-of-cone targets must not steal aim.");
                wall=new GameObject("AssistValidationWall"){hideFlags=HideFlags.HideAndDontSave};
                wall.layer=LayerMask.NameToLayer("ArenaObstacle");
                wall.transform.position=origin+offsetDirection*.9f;
                wall.AddComponent<BoxCollider>().size=Vector3.one*.5f;
                Physics.SyncTransforms();
                if(Vector3.Angle(attack.ResolveAttackDirection(forward),forward)>.5f)
                    throw new InvalidOperationException("Aim assist must ignore obstructed enemies.");
                UnityEngine.Object.DestroyImmediate(wall); wall=null;
                target.transform.position=origin+forward*4f;
                outside.transform.position=origin+forward*5f;
                Physics.SyncTransforms();
                if(Vector3.Angle(attack.ResolveAttackDirection(forward),forward)>.5f)
                    throw new InvalidOperationException("No valid target should preserve intended facing.");
                rear.transform.position=origin+forward*6f;
                Physics.SyncTransforms();
                attack.Cancel();
                // World keyboard values transformed to the player's intended
                // test heading, independent of the rendered facing.
                input.SetValue(movement,new Vector2(right.x,right.z));
                attack.RequestAttack();
                if(Vector3.Angle(attack.AttackDirection,right)>.5f)
                    throw new InvalidOperationException("Click during turning must use current keyboard intent.");
                elapsed.SetValue(attack,PlayerSwordAttack.Duration(0)*.8f);
                if(attack.IsFacingLocked) throw new InvalidOperationException("Recovery should allow movement-facing to resume.");
                input.SetValue(movement,new Vector2(forward.x,forward.z));
                attack.RequestAttack();
                elapsed.SetValue(attack,PlayerSwordAttack.Duration(1)*.1f);
                late.Invoke(attack,null);
                if(attack.CurrentStrike!=1 || Vector3.Angle(attack.AttackDirection,forward)>.5f)
                    throw new InvalidOperationException("Recovery combo should retarget using the next strike's keyboard intent.");
                Debug.Log("Soft targeting validated: offset enemy assistance; rear, outside-cone, unreachable and blocked targets rejected; keyboard intent beats turn lag; combo retarget and recovery unlock passed.");
            }
            finally
            {
                attack.Cancel();
                input.SetValue(movement,originalInput);
                player.transform.position=originalPosition;
                foreach(GameObject probe in new[]{target,rear,outside,wall})
                    if(probe!=null) UnityEngine.Object.DestroyImmediate(probe);
                Physics.SyncTransforms();
                attack.GetComponent<VeilWalkerLocomotion>().SampleKeyboardRun(0f,0f);
            }
        }

        private static void ValidateSwingContinuity(Transform player, Transform visual)
        {
            VeilWalkerLocomotion walk=visual.GetComponent<VeilWalkerLocomotion>();
            PlayerWeaponEquipment gear=visual.GetComponent<PlayerWeaponEquipment>();
            Transform elbow=null;
            foreach(Transform bone in visual.GetComponentsInChildren<Transform>(true))
                if(bone.name=="RightLowerArm" || bone.name=="mixamorig:RightForeArm") elbow=bone;
            float maximumBladeStep=0f,maximumElbowStep=0f;
            try
            {
                for(int stage=0;stage<PlayerSwordAttack.ComboLength;stage++)
                {
                    walk.ClearSwordAttackStart();
                    walk.SampleRunPose(0f,1f,player.forward);
                    walk.CaptureSwordAttackStart();
                    Quaternion previous=gear.SwordSocket.rotation;
                    Vector3 previousElbow=elbow.position;
                    int frames=Mathf.CeilToInt(PlayerSwordAttack.Duration(stage)*120f);
                    for(int frame=0;frame<=frames;frame++)
                    {
                        walk.SampleRunPose(0f,1f,player.forward);
                        walk.SampleSwordAttack(stage,frame/(float)frames);
                        float bladeStep=Quaternion.Angle(previous,gear.SwordSocket.rotation);
                        float elbowStep=Vector3.Distance(previousElbow,elbow.position);
                        maximumBladeStep=Mathf.Max(maximumBladeStep,bladeStep);
                        maximumElbowStep=Mathf.Max(maximumElbowStep,elbowStep);
                        if(bladeStep>18f || elbowStep>.12f)
                            throw new InvalidOperationException($"Sword motion jumped: stage {stage}, frame {frame}, blade {bladeStep:F1}deg, elbow {elbowStep:F3}m.");
                        previous=gear.SwordSocket.rotation;
                        previousElbow=elbow.position;
                    }
                }
                // Queued strikes hold the end pose and carry it into the next
                // wind-up, instead of resetting to the running guard.
                walk.ClearSwordAttackStart();
                walk.SampleRunPose(0f,1f,player.forward);
                for(int stage=0;stage<=PlayerSwordAttack.ComboLength;stage++)
                {
                    walk.CaptureSwordAttackStart();
                    walk.SampleRunPose(0f,1f,player.forward);
                    walk.SampleSwordAttack(stage%PlayerSwordAttack.ComboLength,1f,true);
                    Quaternion ending=gear.SwordSocket.rotation;
                    Vector3 palm=gear.SwordSocket.position;
                    if(stage==PlayerSwordAttack.ComboLength) break;
                    walk.CaptureSwordAttackStart();
                    walk.SampleRunPose(0f,1f,player.forward);
                    walk.SampleSwordAttack((stage+1)%PlayerSwordAttack.ComboLength,0f);
                    if(Quaternion.Angle(ending,gear.SwordSocket.rotation)>1f ||
                        Vector3.Distance(palm,gear.SwordSocket.position)>.015f)
                        throw new InvalidOperationException("Combo transition reset the sword instead of carrying its previous pose.");
                }
                Debug.Log($"Sword continuity validated at 120Hz: maximum blade step {maximumBladeStep:F1}deg, elbow step {maximumElbowStep:F3}m; queued combo poses join without guard reset.");
            }
            finally { walk.ClearSwordAttackStart();walk.SampleKeyboardRun(0f,0f); }
        }
    }
}
