using System;
using System.IO;
using System.Reflection;
using Survivor.Combat;
using Survivor.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class PlayerMouseAimValidation
    {
        const string Pending="Assets/Editor/PlayerMouseAim.pending";
        static PlayerMouseAimValidation() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
            try { Validate(); }
            catch(Exception exception) { Debug.LogException(exception); }
            finally { AssetDatabase.DeleteAsset(Pending); }
        }
        [MenuItem("Survivor/Validate Mouse Aiming")]
        public static void Validate()
        {
            var obj=new GameObject("MouseAimValidationCamera");
            var camera=obj.AddComponent<Camera>(); camera.enabled=false;
            try
            {
                camera.pixelRect=new Rect(0,0,800,600); camera.orthographicSize=10;
                Vector3 player=new Vector3(1000,2.3f,1000);
                obj.transform.position=player+new Vector3(0,12,-12); obj.transform.LookAt(player);
                foreach(bool orthographic in new[]{true,false})
                {
                    camera.orthographic=orthographic;
                    for(int heading=0;heading<16;heading++)
                    {
                        Vector3 intended=Quaternion.Euler(0,heading*22.5f,0)*Vector3.forward;
                        Vector3 screen=camera.WorldToScreenPoint(player+intended*4);
                        if(!PlayerAim.TryProjectDirection(camera.ScreenPointToRay(screen),player,out Vector3 actual) || Vector3.Angle(actual,intended)>.1f)
                            throw new InvalidOperationException("Mouse projection did not match cursor heading");
                    }
                }
                if(PlayerAim.TryProjectDirection(new Ray(player+Vector3.up,Vector3.right),player,out _) ||
                    PlayerAim.TryProjectDirection(new Ray(player+Vector3.up,Vector3.up),player,out _) ||
                    PlayerAim.TryProjectDirection(new Ray(player+Vector3.up,Vector3.down),player,out _))
                    throw new InvalidOperationException("Parallel/behind-camera/center dead-zone aiming should preserve facing");
                Debug.Log("MOUSE AIM PASS: 32 orthographic/perspective cursor headings, parallel and backwards rays, and center dead zone. Gameplay uses the same projection for facing and sword intent.");
                ValidateAttack(camera);
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }
        static void ValidateAttack(Camera camera)
        {
            var movement=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            if(movement==null || Mouse.current==null) return;
            var aim=movement.GetComponent<PlayerAim>();
            var attack=movement.GetComponentInChildren<PlayerSwordAttack>();
            if(aim==null || attack==null) return;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var cameraField=typeof(PlayerAim).GetField("aimCamera",flags);
            var inputField=typeof(PlayerMovement).GetField("moveInput",flags);
            var previousCamera=cameraField.GetValue(aim); var previousInput=inputField.GetValue(movement);
            Mouse previousMouse=Mouse.current;
            Mouse testMouse=null;
            Vector3 previousPosition=movement.transform.position; Quaternion previousRotation=movement.transform.rotation;
            try
            {
                movement.transform.SetPositionAndRotation(new Vector3(11000,0,11000),Quaternion.identity);
                camera.pixelRect=new Rect(0,0,800,600); camera.orthographic=true;
                camera.transform.position=movement.transform.position+new Vector3(0,12,-12);
                camera.transform.LookAt(movement.transform.position+Vector3.right*4);
                // A virtual device keeps this probe independent of the user's
                // real cursor being outside the Game view. Never move the OS pointer.
                testMouse=InputSystem.AddDevice<Mouse>("AimValidationMouse");
                Vector2 cursor=camera.pixelRect.center;
                InputSystem.QueueStateEvent(testMouse,new MouseState {position=cursor});
                InputSystem.Update(); testMouse.MakeCurrent();
                cameraField.SetValue(aim,camera);
                if(!PlayerAim.TryProjectDirection(camera.ScreenPointToRay(cursor),movement.transform.position,out Vector3 expected))
                    throw new InvalidOperationException("Mouse attack probe could not project cursor");
                movement.transform.rotation=Quaternion.LookRotation(-expected,Vector3.up);
                inputField.SetValue(movement,new Vector2(-expected.x,-expected.z));
                attack.Initialize(); attack.Cancel(); attack.RequestAttack();
                if(!attack.IsAttacking || Vector3.Angle(attack.AttackDirection,expected)>.2f)
                    throw new InvalidOperationException($"Cursor attack mismatch: attacking={attack.IsAttacking}, mouse={aim.UsesMouseAim}, input={aim.InputDirection}, strike={attack.AttackDirection}, cursor={cursor}, cameraRect={camera.pixelRect}, movement={movement.enabled}");
                Debug.Log("MOUSE ATTACK PASS: cursor opposes both WASD input and current player facing; actual sword attack commits toward the cursor.");
            }
            finally
            {
                attack.Cancel(); cameraField.SetValue(aim,previousCamera); inputField.SetValue(movement,previousInput);
                if(testMouse!=null) InputSystem.RemoveDevice(testMouse);
                if(previousMouse!=null && previousMouse.added) previousMouse.MakeCurrent();
                movement.transform.SetPositionAndRotation(previousPosition,previousRotation);
                var walk=attack.GetComponent<VeilWalkerLocomotion>(); if(walk!=null) walk.SampleKeyboardRun(0,0);
            }
        }
    }
}
