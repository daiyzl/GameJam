#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit request-file bridge: no work occurs on normal imports without a request.
[InitializeOnLoad]
public static class PrototypeEditorBridge
{
    private const string Prefix = "FiveElementsVerification.";
    public const string ScenePath = "Assets/Scenes/FiveElementsPrototype.unity";
    private static readonly string RequestPath = Path.Combine(Application.dataPath, "../PrototypeVerification/request.json");
    private static int unexpectedErrors;

    [Serializable] private class Request { public string id = ""; public string outputDirectory = ""; }
    [Serializable] private class VerificationReport
    {
        public string status, projectPath, scenePath, checks, screenshotPath, utcTime;
        public int runtimeFrames, unexpectedErrors;
    }

    static PrototypeEditorBridge()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += OnLog;
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (SessionState.GetString(Prefix + "phase", "") != "" &&
            (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) unexpectedErrors++;
    }

    [MenuItem("五行原型/打开验证场景")]
    public static void OpenScene()
    {
        if (EditorApplication.isPlaying) { Debug.Log("请退出 Play 模式后打开场景。"); return; }
        EnsureScene(); ShowGameView();
    }

    [MenuItem("五行原型/运行规则检查")]
    public static void CheckRules() { Debug.Log(SimulationVerification.RunAll()); }

    private static void EnsureScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            else
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var cameraObject = new GameObject("Prototype Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.075f, 0.095f);
                cameraObject.transform.position = new Vector3(4, 2, -10);
                var root = new GameObject("Five Elements Prototype");
                root.AddComponent<GameRuler>(); root.AddComponent<PrototypeView>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
            }
        }
        SceneManager.SetActiveScene(scene);
    }

    private static void ShowGameView()
    {
        Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (type != null) { EditorWindow window = EditorWindow.GetWindow(type); window.Show(); window.maximized = true; window.Focus(); }
    }
    private static GameRuler FindController()
    {
        foreach (GameRuler ruler in UnityEngine.Object.FindObjectsOfType<GameRuler>())
            if (ruler.gameObject.scene.path == ScenePath && ruler.GetComponent<PrototypeView>() != null) return ruler;
        return null;
    }
    private static void Require(bool condition, string description)
    { if (!condition) throw new InvalidOperationException("Runtime verification failed: " + description); }

    private static void Update()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            string phase = SessionState.GetString(Prefix + "phase", "");
            if (phase == "")
            {
                if (!File.Exists(RequestPath)) return;
                Request request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                if (request == null || string.IsNullOrEmpty(request.outputDirectory)) throw new InvalidOperationException("Invalid verification request.");
                SessionState.SetString(Prefix + "output", request.outputDirectory);
                Directory.CreateDirectory(request.outputDirectory);
                File.Delete(RequestPath);
                SessionState.SetString(Prefix + "checks", SimulationVerification.RunAll());
                EnsureScene(); ShowGameView();
                unexpectedErrors = 0;
                SessionState.SetString(Prefix + "phase", "waitingPlay");
                SessionState.SetFloat(Prefix + "deadline", (float)EditorApplication.timeSinceStartup + 90);
                EditorApplication.isPlaying = true;
                return;
            }
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "deadline", 0))
                throw new TimeoutException("Unity Play verification exceeded its deadline.");
            if (!EditorApplication.isPlaying) return;
            GameRuler controller = FindController();
            if (controller == null) return;

            if (phase == "waitingPlay")
            {
                var runtime = new StringBuilder();
                Require(controller.IsPlacement, "Starts in placement mode");
                Require(!controller.TryAdjustPlan(controller.Simulation.Level.SourceIndex, Element.Gold, 1), "Source rejects placement");
                controller.LoadDemoPlan();
                Require(controller.Remaining(Element.Gold) == 0, "Budget is spent exactly");
                Require(!controller.TryAdjustPlan(controller.Simulation.Level.Index(3, 1), Element.Gold, 1), "Overspending is rejected");
                controller.StepOnce();
                Require(!controller.TryAdjustPlan(controller.Simulation.Level.Index(3, 1), Element.Gold, -1), "Paused simulation still locks placement");
                controller.ResetSimulation();
                Require(controller.IsPlacement && controller.Planned(controller.Simulation.Level.Index(3, 1), Element.Gold) == 6, "Retry retains plan");
                while (controller.Simulation.Result == SimulationResult.Active) controller.StepOnce();
                Require(controller.Simulation.Result == SimulationResult.Won, "Level 1 through controller wins");
                runtime.AppendLine("PASS Play controller: placement, budget, locking, single step, retry, level 1 win");
                controller.LoadLevel(1); controller.LoadDemoPlan();
                while (controller.Simulation.Result == SimulationResult.Active) controller.StepOnce();
                Require(controller.Simulation.Result == SimulationResult.Won, "Level 2 through controller wins");
                runtime.AppendLine("PASS Play controller: level switching and protected level 2 win");
                controller.LoadLevel(0);
                while (controller.Simulation.Result == SimulationResult.Active) controller.StepOnce();
                Require(controller.Simulation.Result == SimulationResult.Lost, "No placement times out");
                controller.ResetSimulation();
                Require(controller.Simulation.StepCount == 0 && controller.Simulation.Result == SimulationResult.Active, "Reset clears failure");
                runtime.AppendLine("PASS Play controller: timeout loss and restart");
                SessionState.SetString(Prefix + "checks", SessionState.GetString(Prefix + "checks", "") + runtime);
                controller.LoadDemoPlan(); controller.ToggleSpeed(); controller.ResumeSimulation();
                SessionState.SetString(Prefix + "phase", "scheduler");
                SessionState.SetInt(Prefix + "startFrame", Time.frameCount);
                return;
            }
            if (phase == "scheduler")
            {
                if (controller.Simulation.StepCount < 3) return;
                Require(Time.frameCount > SessionState.GetInt(Prefix + "startFrame", 0), "Unity Update really advanced frames");
                controller.PauseSimulation();
                SessionState.SetString(Prefix + "checks", SessionState.GetString(Prefix + "checks", "") + "PASS actual Unity Update: timed stepping at x4\n");
                controller.ToggleSpeed(); controller.LoadLevel(2); controller.StepOnce();
                ShowGameView();
                SessionState.SetInt(Prefix + "captureFrame", Time.frameCount + 10);
                SessionState.SetString(Prefix + "phase", "captureLab");
                return;
            }
            if (phase == "captureLab")
            {
                if (Time.frameCount < SessionState.GetInt(Prefix + "captureFrame", 0)) return;
                ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Prefix + "output", ""), "laboratory.png"));
                SessionState.SetString(Prefix + "phase", "waitLab");
                return;
            }
            if (phase == "waitLab")
            {
                string labImage = Path.Combine(SessionState.GetString(Prefix + "output", ""), "laboratory.png");
                if (!File.Exists(labImage) || new FileInfo(labImage).Length < 100) return;
                controller.LoadLevel(0); controller.LoadDemoPlan();
                SessionState.SetInt(Prefix + "captureFrame", Time.frameCount + 10);
                SessionState.SetString(Prefix + "phase", "captureMain");
                return;
            }
            if (phase == "captureMain")
            {
                if (Time.frameCount < SessionState.GetInt(Prefix + "captureFrame", 0)) return;
                ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Prefix + "output", ""), "play-preview.png"));
                SessionState.SetString(Prefix + "phase", "finish");
                return;
            }
            if (phase == "finish")
            {
                string image = Path.Combine(SessionState.GetString(Prefix + "output", ""), "play-preview.png");
                if (!File.Exists(image) || new FileInfo(image).Length < 100) return;
                Require(unexpectedErrors == 0, "No runtime exceptions or errors");
                WriteReport("passed", "");
                SessionState.SetString(Prefix + "phase", "");
                Debug.Log("五行原型验证通过。已保留 Play 模式和第一关布置，可以点击启动继续验证。");
            }
        }
        catch (Exception exception)
        {
            WriteReport("failed", exception.ToString());
            SessionState.SetString(Prefix + "phase", "");
            Debug.LogException(exception);
        }
    }
    private static void WriteReport(string status, string failure)
    {
        string output = SessionState.GetString(Prefix + "output", "");
        if (string.IsNullOrEmpty(output)) return;
        Directory.CreateDirectory(output);
        var report = new VerificationReport
        {
            status = status, projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")), scenePath = ScenePath,
            checks = SessionState.GetString(Prefix + "checks", "") + failure,
            screenshotPath = Path.Combine(output, "play-preview.png"), utcTime = DateTime.UtcNow.ToString("O"),
            runtimeFrames = Time.frameCount, unexpectedErrors = unexpectedErrors
        };
        File.WriteAllText(Path.Combine(output, "verification-report.json"), JsonUtility.ToJson(report, true));
    }
}
#endif
