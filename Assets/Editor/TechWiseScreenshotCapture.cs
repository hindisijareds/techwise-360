using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Editor-only screenshots of the actual VR presentation. No scene or runtime asset changes.
public static class TechWiseScreenshotCapture
{
    const string Key="TechWise.ScreenshotCapture";
    const string Output="Screenshots/VR-Modes";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly string[] Strings={MainMenu.ControlModeKey,TechWiseSimulationModeManager.GameModeKey,TechWiseSimulationModeManager.SimulationTypeKey,TechWiseSimulationModeManager.CompetitionIdKey,TechWiseSimulationModeManager.CompetitionTitleKey};
    static readonly string[] Ints={"TechWise360.ControlModeExplicit","TechWise360.ControlsGuideVisible","TechWise360.WristShortcutHidden"};
    static readonly Stack<IEnumerator> flow=new();
    static int frame=-1; static double deadline;
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        foreach(var key in Strings){SessionState.SetBool(Key+key,PlayerPrefs.HasKey(key));SessionState.SetString(Key+key+"value",PlayerPrefs.GetString(key));}
        foreach(var key in Ints){SessionState.SetBool(Key+key,PlayerPrefs.HasKey(key));SessionState.SetInt(Key+key+"value",PlayerPrefs.GetInt(key));}
        PlayerPrefs.SetString(MainMenu.ControlModeKey,MainMenu.VrModeValue);
        PlayerPrefs.SetInt("TechWise360.ControlsGuideVisible",0);
        PlayerPrefs.SetInt("TechWise360.WristShortcutHidden",1);
        EditorSceneManager.OpenScene("Assets/Scenes/Singleplayer.unity");
        CaptureCorner();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var menu=UnityEngine.Object.FindAnyObjectByType<MainMenu>();
        typeof(MainMenu).GetField("useUiToolkitMenu",Hidden)?.SetValue(menu,false);
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Isolate()
    {
        if(!SessionState.GetBool(Key,false))return;
        TechWiseOfflineAttemptQueue.VerificationQueuePath=Path.GetFullPath(Output+"/capture-empty-queue.json");
        File.WriteAllText(TechWiseOfflineAttemptQueue.VerificationQueuePath,"{\"attempts\":[],\"acknowledged\":[]}");
        TechWiseAttemptRecorder.LocalVerificationMode=true;
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        EditorApplication.playModeStateChanged+=mode=>{
            if(mode!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            var queue=TechWiseOfflineAttemptQueue.EnsureInstance();queue.StopAllCoroutines();queue.enabled=false;
            SceneManager.sceneLoaded+=DisableHands;DisableHands(SceneManager.GetActiveScene(),LoadSceneMode.Single);
            deadline=EditorApplication.timeSinceStartup+600;flow.Push(CaptureAll());EditorApplication.update+=Tick;
        };
    }
    static void DisableHands(Scene scene,LoadSceneMode mode)
    {
        foreach(var hand in UnityEngine.Object.FindObjectsByType<XRBaseInteractor>())if(hand is not XRSocketInteractor)hand.enabled=false;
    }
    static void Tick()
    {
        try{
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Screenshot capture timed out");
            EditorApplication.isPaused=false;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
            if(frame==Time.frameCount)return;frame=Time.frameCount;
            while(flow.Count>0){var next=flow.Peek();if(!next.MoveNext()){flow.Pop();continue;}if(next.Current is IEnumerator nested){flow.Push(nested);continue;}return;}
            Finish(null);
        }catch(Exception error){Finish(error);}
    }
    static void Finish(Exception error)
    {
        EditorApplication.update-=Tick;SessionState.SetBool(Key,false);
        foreach(var key in Strings)if(SessionState.GetBool(Key+key,false))PlayerPrefs.SetString(key,SessionState.GetString(Key+key+"value",""));else PlayerPrefs.DeleteKey(key);
        foreach(var key in Ints)if(SessionState.GetBool(Key+key,false))PlayerPrefs.SetInt(key,SessionState.GetInt(Key+key+"value",0));else PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();Time.timeScale=1;
        File.WriteAllText(Output+"/capture-result.txt",error==null?"Unity VR previews at 1920 x 1200. Modes and menus: Play Mode. Knowledge Corner: authored scene before simulation. Not headset recordings. No scene or gameplay assets modified.":error.ToString());
        EditorApplication.Exit(error==null?0:1);
    }
    static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Hidden).Invoke(target,args);
    static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
    static IEnumerator Load(string scene,string mode)
    {
        TechWiseSimulationModeManager.SetMode("assembly",mode);
        var guidType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Tutorials.Core.SceneObjectGuidManager")).FirstOrDefault(t=>t!=null);
        if(guidType!=null){var manager=guidType.GetProperty("Instance").GetValue(null);((IDictionary)guidType.GetField("m_Components",Hidden).GetValue(manager)).Clear();}
        var task=SceneManager.LoadSceneAsync(scene);while(!task.isDone)yield return null;
        yield return Frames(100);
    }
    static IEnumerator CaptureAll()
    {
        yield return Frames(100);
        var menu=UnityEngine.Object.FindAnyObjectByType<MainMenu>();Call(menu,"ConfigureVrMenu",Camera.main);yield return Frames(5);
        CapturePanel("01-main-menu.png",(RectTransform)((Canvas)typeof(MainMenu).GetField("rootCanvas",Hidden).GetValue(menu)).transform,1.10f);
        yield return Load("Singleplayer","tutorial");
        var lesson=UnityEngine.Object.FindAnyObjectByType<TechWiseTutorialRuntime>();Call(lesson,"BeginControls");yield return Frames(15);
        var controls=GameObject.Find("TechWise VR Controls Guide");
        if(controls!=null)controls.transform.position+=Vector3.right*4;
        CapturePanel("02-tutorial-controls.png",GameObject.Find("Tutorial lesson panel").GetComponent<RectTransform>(),1.3f);
        Call(lesson,"SkipControls");yield return Frames(25);
        CapturePanel("03-tutorial-assembly.png",GameObject.Find("Tutorial lesson panel").GetComponent<RectTransform>(),1.3f);
        yield return Load("Multiplayer","practice");
        var state=TechWiseSimulationRuntime.Instance;
        var board=state.FindPart("Motherboard").transform.position;
        var parts=state.Parts.Where(p=>p.gameObject.activeInHierarchy).ToArray();var center=parts.Select(p=>p.transform.position).Aggregate(Vector3.zero,(a,v)=>a+v)/parts.Length;
        Capture("04-practice-mode.png",center+Vector3.back*1.85f+Vector3.up*.75f,center+Vector3.up*.22f,65);
        // Move the draggable guide aside, like a user can, for an unobstructed menu view.
        var practiceGuide=GameObject.Find("TechWise Practice Side Guide");
        if(practiceGuide!=null)practiceGuide.transform.position+=Vector3.right*4;
        var pause=UnityEngine.Object.FindAnyObjectByType<TechWiseInGameMenu>();Call(pause,"SetMenuOpen",true);yield return Frames(3);
        var pauseCanvas=(Canvas)typeof(TechWiseInGameMenu).GetField("canvas",Hidden).GetValue(pause);
        CapturePanel("07-pause-menu.png",pauseCanvas.GetComponent<RectTransform>(),1.04f);
        Call(pause,"ShowSettingsHelp");yield return Frames(3);
        CapturePanel("08-settings-menu.png",pauseCanvas.GetComponent<RectTransform>(),1.04f);
        Call(pause,"SetMenuOpen",false);
        yield return Load("Multiplayer","competition");
        var recorder=UnityEngine.Object.FindAnyObjectByType<TechWiseAttemptRecorder>();recorder.StartCompetitionAttempt();yield return Frames(15);
        var display=GameObject.Find("Verification display").GetComponent<RectTransform>();
        CapturePanel("06-competition-mode.png",display,2.25f);
    }
    static void CaptureCorner()
    {
        var corner=GameObject.Find("Knowledge Corner");
        var text=corner.GetComponentsInChildren<TMPro.TMP_Text>().FirstOrDefault(t=>t.text.IndexOf("Knowledge",StringComparison.OrdinalIgnoreCase)>=0);
        var points=new List<Vector3>();
        foreach(var rect in corner.GetComponentsInChildren<RectTransform>())
        {
            if(rect.GetComponent<UnityEngine.UI.Graphic>()==null)continue;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);points.AddRange(corners);
        }
        var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);
        var direction=text!=null?-text.transform.forward:Vector3.back;
        direction=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
        var right=Vector3.Cross(Vector3.up,-direction);
        float width=points.Max(p=>Vector3.Dot(p,right))-points.Min(p=>Vector3.Dot(p,right));
        float distance=Mathf.Max(b.size.y,width/1.6f)*.5f/Mathf.Tan(55*Mathf.Deg2Rad*.5f)*1.25f;
        File.WriteAllText(Output+"/corner-layout.txt","Bounds="+b+" direction="+direction+" width="+width+"\n"+string.Join("\n",corner.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.name+" pos="+t.transform.position+" forward="+t.transform.forward+" text="+t.text)));
        Capture("05-knowledge-corner.png",b.center+direction*distance+Vector3.up*.12f,b.center,55);
    }
    static void CapturePanel(string filename,RectTransform panel,float padding)
    {
        Canvas.ForceUpdateCanvases();var corners=new Vector3[4];panel.GetWorldCorners(corners);
        var center=(corners[0]+corners[2])*.5f;float height=Vector3.Distance(corners[0],corners[1]);float width=Vector3.Distance(corners[1],corners[2]);
        float distance=Mathf.Max(height,width/1.6f)*.5f/Mathf.Tan(50*Mathf.Deg2Rad*.5f)*padding;
        Capture(filename,center-panel.forward*distance,center,50);
    }
    static void Capture(string filename,Vector3 position,Vector3 target,float fov)
    {
        var obj=new GameObject("Screenshot camera");var camera=obj.AddComponent<Camera>();
        if(Camera.main!=null)camera.CopyFrom(Camera.main);
        camera.stereoTargetEye=StereoTargetEyeMask.None;camera.nearClipPlane=.01f;camera.fieldOfView=fov;camera.aspect=1.6f;
        camera.transform.position=position;camera.transform.LookAt(target,Vector3.up);
        var rt=new RenderTexture(1920,1200,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1920,1200,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,1920,1200),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+filename,tex.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.Destroy(tex);UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(obj);
        Debug.Log("SCREENSHOT_SAVED "+filename);
    }
}
