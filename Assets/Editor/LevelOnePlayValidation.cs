using System;
using System.IO;
using Survivor.CameraSystem;
using Survivor.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Survivor.EditorTools
{
    [InitializeOnLoad]
    public static class LevelOnePlayValidation
    {
        const string Pending="Assets/Editor/LevelOnePlayValidation.pending";
        const string Phase="Survivor.Level01Probe.Phase",Wait="Survivor.Level01Probe.Wait",Deadline="Survivor.Level01Probe.Deadline";
        static Keyboard keyboard,previousKeyboard;
        static Vector3 start,cameraStart,dashStart;
        static bool dashObserved;
        static PlayerMovement player;
        static LevelOnePlayValidation() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                int phase=SessionState.GetInt(Phase,0); double now=EditorApplication.timeSinceStartup;
                if(phase==0)
                {
                    if(EditorApplication.isPlaying || SceneManager.GetActiveScene().name!="Game")
                        throw new InvalidOperationException("Level 1 test starts in Edit mode in Game scene.");
                    SessionState.SetInt(Phase,1); SessionState.SetFloat(Wait,(float)now+2);
                    SessionState.SetFloat(Deadline,(float)now+60); EditorApplication.isPlaying=true; return;
                }
                if(now>SessionState.GetFloat(Deadline,0)) throw new InvalidOperationException("Level 1 play test timed out.");
                if(!EditorApplication.isPlaying || now<SessionState.GetFloat(Wait,0)) return;
                var camera=Camera.main;
                if(phase==1)
                {
                    player=UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
                    var root=GameObject.Find("Level01_CrimsonVillage");
                    if(player==null || root==null || camera==null || camera.GetComponent<LevelOneCamera>()==null)
                        throw new InvalidOperationException("Level 1 runtime references missing.");
                    Vector3 forward=camera.transform.forward; forward.y=0; forward.Normalize();
                    Vector3 right=camera.transform.right; right.y=0; right.Normalize();
                    if(Vector3.Dot(player.WorldInput(Vector2.up),forward)<.99f || Vector3.Dot(player.WorldInput(Vector2.right),right)<.99f)
                        throw new InvalidOperationException("WASD does not match the approved camera.");
                    Vector3 screen=camera.WorldToViewportPoint(player.transform.position);
                    if(screen.z<=0 || screen.x<.58f || screen.x>.72f)
                        throw new InvalidOperationException("Right-shifted intro composition was lost: "+screen);
                    start=player.transform.position; cameraStart=camera.transform.position;
                    LevelOneSceneInstaller.RenderPreview(camera);
                    previousKeyboard=Keyboard.current; keyboard=InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                    SessionState.SetInt(Phase,2); SessionState.SetFloat(Wait,(float)now+.7f); return;
                }
                if(phase==2)
                {
                    Vector3 delta=player.transform.position-start;
                    if(Vector3.Dot(delta,player.WorldInput(Vector2.up))<.5f) throw new InvalidOperationException("W did not move the player along the route.");
                    if(Vector3.Distance(cameraStart,camera.transform.position)<.2f) throw new InvalidOperationException("Level camera did not follow movement.");
                    dashStart=player.transform.position; dashObserved=false; player.DashStarted+=ObserveDash;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Space));
                    SessionState.SetInt(Phase,3); SessionState.SetFloat(Wait,(float)now+.4f); return;
                }
                if(phase==3)
                {
                    if(!dashObserved || Vector3.Distance(dashStart,player.transform.position)<1)
                        throw new InvalidOperationException($"Dash did not travel along the level road: observed={dashObserved}, distance={Vector3.Distance(dashStart,player.transform.position):F2}");
                    if(player.GetComponent<PlayerHealth>().IsDead || !Physics.Raycast(player.transform.position,Vector3.down,2,1,QueryTriggerInteraction.Ignore))
                        throw new InvalidOperationException("Player left the level floor during movement/dash.");
                    Debug.Log("LEVEL01 PLAY PASS: real W input moved along road, right-shifted camera followed, real Space input dashed safely, player remained alive on floor.");
                    Finish();
                }
            }
            catch(Exception exception) { Debug.LogException(exception); Finish(); }
        }
        static void ObserveDash(float duration) { dashObserved=true; }
        static void Finish()
        {
            if(player!=null) player.DashStarted-=ObserveDash;
            if(keyboard!=null && keyboard.added)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); InputSystem.RemoveDevice(keyboard);
            }
            keyboard=null; if(previousKeyboard!=null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            SessionState.SetInt(Phase,0); AssetDatabase.DeleteAsset(Pending); if(File.Exists(Pending)) File.Delete(Pending);
            if(EditorApplication.isPlaying) EditorApplication.isPlaying=false;
        }
    }
}
