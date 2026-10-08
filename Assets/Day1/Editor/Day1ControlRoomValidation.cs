using System;
using System.IO;
using System.Linq;
using Emerge.Day1.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Day1.Editor
{
    [InitializeOnLoad]
    public static class Day1ControlRoomValidation
    {
        private const string Prefix="Light.Day1.ControlRoom.Validation.";
        static Day1ControlRoomValidation()
        {EditorApplication.playModeStateChanged+=StateChanged;EditorApplication.update+=Update;}
        [MenuItem("Tools/剧情/Day1/验证总控室碰撞与淡出")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
                throw new InvalidOperationException("请等待编辑器就绪。");
            if(SceneManager.GetActiveScene().path!=Day1SceneSetup.ScenePath || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("请先打开并保存 Day1。");
            SessionState.SetBool(Prefix+"Pending",true);
            SessionState.SetBool(Prefix+"Suppression",SessionState.GetBool("Light.GameFlow.SuppressForValidation",false));
            SessionState.SetBool(Prefix+"Background",Application.runInBackground);
            SessionState.SetFloat(Prefix+"Started",(float)EditorApplication.timeSinceStartup);
            SessionState.SetBool("Light.GameFlow.SuppressForValidation",true);
            Application.runInBackground=true;
            EditorApplication.isPaused=false;EditorApplication.EnterPlaymode();
        }
        private static void StateChanged(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Prefix+"Pending",false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                var flow=UnityEngine.Object.FindObjectOfType<Day1FlowController>();
                flow.enabled=false;
                Day1ControlRoomSelfTest.Completed-=Complete;Day1ControlRoomSelfTest.Completed+=Complete;
                new GameObject("总控室视觉与物理验证（临时）").AddComponent<Day1ControlRoomSelfTest>();
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool("Light.GameFlow.SuppressForValidation",SessionState.GetBool(Prefix+"Suppression",false));
                Application.runInBackground=SessionState.GetBool(Prefix+"Background",false);
                SessionState.SetBool(Prefix+"Pending",false);
                Day1ControlRoomSelfTest.Completed-=Complete;
            }
        }
        private static void Complete(bool passed) {EditorApplication.ExitPlaymode();}
        private static void Update()
        {
            if(!SessionState.GetBool(Prefix+"Pending",false))return;
            EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Prefix+"Started",0)>100)
            {Debug.LogError("CONTROLROOM_VALIDATION_TIMEOUT");EditorApplication.ExitPlaymode();}
        }
    }
}
