using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelPrototype;
using UnityEngine;

namespace Emerge.Day1.Tests
{
    /// <summary>Real Play-mode renderer/physics checks; no player saves or story rewards are touched.</summary>
    public sealed class Day1ControlRoomSelfTest : MonoBehaviour
    {
        [Serializable] public sealed class Check {public string name,observed;public bool passed;}
        [Serializable] public sealed class Report {public bool passed;public string completedUtc;public List<Check> checks=new List<Check>();}
        public static event Action<bool> Completed;
        private Report report=new Report();
        private Day1FlowController flow;
        private PlayerMovement movement;
        private Camera camera;
        private IEnumerator Start()
        {
            flow=FindObjectOfType<Day1FlowController>(); movement=flow.actor.GetComponent<PlayerMovement>();camera=Camera.main;
            flow.actor.CancelDialogue();
            // These gates belong to admission/story progression, verified by Day1SelfTest.
            // This isolated run measures the permanent corridor and wall geometry.
            if(flow.admissionGate!=null)flow.admissionGate.SetActive(false);
            if(flow.outdoorGate!=null)flow.outdoorGate.SetActive(false);
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool more;object wait;
                try {more=stack.Peek().MoveNext();wait=more?stack.Peek().Current:null;}
                catch(Exception e){Add(false,"Validation completed without exception",e.ToString());break;}
                if(!more){stack.Pop();continue;}
                if(wait is IEnumerator nested){stack.Push(nested);continue;}
                yield return wait;
            }
            report.passed=report.checks.Count>0&&report.checks.All(c=>c.passed);report.completedUtc=DateTime.UtcNow.ToString("O");Write();
            movement.ResumeKeyboardInput();
            Debug.Log("CONTROLROOM_VALIDATION_"+(report.passed?"PASS":"FAIL"));Completed?.Invoke(report.passed);
        }
        private IEnumerator Run()
        {
            yield return null;
            Add(flow.controlRoom!=null,"Day1 references the new control room art");
            Add(flow.controlRoom.sourceArtwork.width==5000&&flow.controlRoom.sourceArtwork.height==6000,"Source art keeps its 5:6 aspect and original resolution");
            var walls=FindObjectsOfType<Day1OcclusionFade>().Where(f=>f.name.StartsWith("前墙 ")).ToArray();
            Add(walls.Length>=6,"Lower walls use independent fade segments",walls.Length.ToString());
            var wall=walls.OrderBy(f=>Mathf.Abs((f.groundStart.x+f.groundEnd.x)*.5f-.5f)).First(f=>Mathf.Abs(f.groundStart.y-f.groundEnd.y)<.01f);
            Vector2 ground=(wall.groundStart+wall.groundEnd)*.5f;
            Move(new Vector2(2,1));yield return new WaitForSecondsRealtime(.6f);
            Add(walls.All(f=>Mathf.Abs(f.CurrentAlpha-1)<.01f),"Walls remain opaque away from the player silhouette");
            Move(ground+Vector2.up*.2f);yield return new WaitForSecondsRealtime(.055f);
            Add(wall.CurrentAlpha>wall.occludedAlpha&&wall.CurrentAlpha<.99f,"Fade starts smoothly instead of popping",wall.CurrentAlpha.ToString("F3"));
            yield return new WaitForSecondsRealtime(.35f);
            Add(wall.IsOccluding&&Mathf.Abs(wall.CurrentAlpha-wall.occludedAlpha)<.015f,"A wall covering the player reaches the configured partial opacity",wall.CurrentAlpha.ToString("F3"));
            Add(walls.Any(f=>Mathf.Abs(f.CurrentAlpha-1)<.01f),"Unrelated wall sections stay opaque while one section fades");
            Frame(new Vector2(2,-3.8f),5.3f);Capture("Validation/day1-controlroom-occluded.png");
            Vector2 before=flow.actor.transform.position;
            movement.SetScriptedInput(Vector2.down);yield return new WaitForSeconds(.45f);movement.SetScriptedInput(Vector2.zero);
            Add(flow.actor.transform.position.y>ground.y-.08f,"A faded wall still blocks the real Rigidbody2D",flow.actor.transform.position.ToString());
            Move(new Vector2(2,1));yield return new WaitForSecondsRealtime(.55f);
            Add(Mathf.Abs(wall.CurrentAlpha-1)<.01f,"The wall smoothly restores full opacity after the player leaves");
            Frame(new Vector2(2,-.5f),9.6f);Capture("Validation/day1-controlroom-clear.png",1440,1440);
            var pairs=new[]{
                new[]{new Vector2(-7.55f,4.5f),flow.controlRoom.PixelToWorld(new Vector2(1210,2110))},
                new[]{new Vector2(-7.55f,-.5f),new Vector2(-4.11875f,-1.75f)},
                new[]{new Vector2(2,7.55f),new Vector2(2,4.11875f)},
                new[]{new Vector2(2,-9.55f),new Vector2(2,-7.8125f)},
                new[]{new Vector2(10.55f,.5f),new Vector2(8.125f,-1.75f)}};
            string[] names={"Buffer","Machine room","Living area","Chemical laboratory","Storage"};
            for(int i=0;i<pairs.Length;i++)
            {
                Move(pairs[i][0]);yield return Walk(pairs[i][1],names[i]+" corridor into control room");
                yield return Walk(pairs[i][0],names[i]+" corridor out of control room");
            }
            Add(flow.controlRoom.paintedInteractions.All(p=>p.GetComponentsInChildren<SpriteRenderer>().All(r=>!r.enabled)),"Painted devices replace only their local placeholder visuals");
            Add(flow.interactions.All(p=>p!=null&&p.Prop!=null&&!string.IsNullOrWhiteSpace(p.Prop.InstanceId)),"All story interaction identities survive the art installation");
        }
        private IEnumerator Walk(Vector2 target,string label)
        {
            float deadline=Time.realtimeSinceStartup+3.5f;
            while(Vector2.Distance(flow.actor.transform.position,target)>.12f&&Time.realtimeSinceStartup<deadline)
            {movement.SetScriptedInput((target-(Vector2)flow.actor.transform.position).normalized);yield return new WaitForFixedUpdate();}
            movement.SetScriptedInput(Vector2.zero);
            Add(Vector2.Distance(flow.actor.transform.position,target)<.2f,"Real Physics2D traversal: "+label,flow.actor.transform.position.ToString());
        }
        private void Move(Vector2 p)
        {movement.SetScriptedInput(Vector2.zero);flow.actor.transform.position=p;movement.Body.position=p;movement.Body.velocity=Vector2.zero;Physics2D.SyncTransforms();}
        private void Frame(Vector2 p,float size)
        {camera.GetComponent<CameraFollow>().enabled=false;camera.transform.position=new Vector3(p.x,p.y,-10);camera.orthographicSize=size;}
        private void Capture(string path,int width=1440,int height=1080)
        {
            var active=RenderTexture.active;var old=camera.targetTexture;var rt=new RenderTexture(width,height,24);var png=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,width,height),0,0);png.Apply();File.WriteAllBytes(path,png.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;Destroy(rt);Destroy(png);}
        }
        private void Add(bool passed,string name,string observed="")
        {report.checks.Add(new Check{passed=passed,name=name,observed=observed});Write();Debug.Log("CONTROLROOM_CHECK "+(passed?"PASS ":"FAIL ")+name+": "+observed);}
        private void Write()=>File.WriteAllText("Validation/day1-controlroom-results.json",JsonUtility.ToJson(report,true));
    }
}
