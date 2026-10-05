#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Emerge.Orthographic25D
{
    public sealed class Orthographic25DPreviewChecks : MonoBehaviour
    {
        public static event Action<bool> Completed;
        [Serializable] private sealed class Check { public string name, observed; public bool passed; }
        [Serializable] private sealed class Report { public bool passed; public string completedUtc; public List<Check> checks = new List<Check>(); }
        private readonly Report report = new Report();
        private IEnumerator Start()
        {
            Time.timeScale = 1; var demo = GetComponent<Orthographic25DDemo>(); demo.PreviewRunning = true;
            yield return null; yield return null;
            Add("Orthographic camera and diagonal view", demo.viewCamera.orthographic && Mathf.Abs(demo.viewCamera.transform.eulerAngles.x - 35) < .1f && Mathf.Abs(demo.viewCamera.transform.eulerAngles.y - 45) < .1f);
            Add("Player is a 2D sprite with 3D collision", demo.visual.GetComponent<SpriteRenderer>().sprite != null && GetComponent<CharacterController>() != null);
            Add("Preview does not start the real game or touch user saves", Emerge.GameFlow.GameSessionController.Instance == null);
            demo.Teleport(new Vector3(3, .05f, -4)); Vector3 start = transform.position;
            demo.SetScriptedInput(Vector2.right); yield return new WaitForSeconds(.6f); demo.SetScriptedInput(Vector2.zero);
            Vector3 delta = transform.position - start;
            Add("Right key moves right on the screen", Vector3.Dot(delta, demo.GroundRight) > 1.5f && Mathf.Abs(Vector3.Dot(delta, demo.GroundForward)) < .1f, delta.ToString());
            // Start directly in front of the authored stair opening; walk toward world +Z.
            demo.Teleport(new Vector3(.5f, .05f, -3.8f));
            demo.SetScriptedInput(new Vector2(Vector3.Dot(Vector3.forward, demo.GroundRight), Vector3.Dot(Vector3.forward, demo.GroundForward)));
            yield return new WaitForSeconds(1.85f); demo.SetScriptedInput(Vector2.zero); yield return new WaitForSeconds(.15f);
            Add("Player climbs actual steps and reaches the platform", transform.position.y > 1.5f && transform.position.z > 1.1f, transform.position.ToString());
            demo.Teleport(new Vector3(3.5f, .05f, 4.5f));
            demo.SetScriptedInput(new Vector2(Vector3.Dot(Vector3.forward, demo.GroundRight), Vector3.Dot(Vector3.forward, demo.GroundForward)));
            yield return new WaitForSeconds(.8f); demo.SetScriptedInput(Vector2.zero);
            Add("Solid wall blocks movement", transform.position.z < 5.5f, transform.position.ToString());
            demo.Teleport(demo.spawn); demo.SetScriptedInput(Vector2.zero); demo.follow = false;
            yield return new WaitForSeconds(.25f); yield return new WaitForEndOfFrame();
            Directory.CreateDirectory("Validation"); ScreenCapture.CaptureScreenshot("Validation/orthographic25d-preview.png"); yield return null;
            report.passed = report.checks.TrueForAll(x => x.passed); report.completedUtc = DateTime.UtcNow.ToString("O");
            File.WriteAllText("Validation/orthographic25d-results.json", JsonUtility.ToJson(report, true));
            Debug.Log("ORTHOGRAPHIC25D_PREVIEW_" + (report.passed ? "PASS" : "FAIL"));
            demo.PreviewRunning = false; demo.ResumeInput(); Completed?.Invoke(report.passed);
        }
        private void Add(string name, bool passed, string observed = "") => report.checks.Add(new Check { name = name, passed = passed, observed = observed });
    }
}
#endif
