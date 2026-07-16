using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Flusi.EditorTools
{
    /// Dev tooling: runs the Unity Test Runner programmatically and writes a
    /// compact result summary to Temp/flusi-tests.txt so an external driver
    /// (the MCP bridge) can read pass/fail without the Test Runner UI.
    /// Not shipped in builds (Editor-only assembly).
    ///
    /// The callback is registered once per domain load via [InitializeOnLoad]
    /// rather than at Run() time, so results are still captured when a PlayMode
    /// run reloads the domain on entering play mode.
    [InitializeOnLoad]
    public class FlusiTestRunner : ICallbacks
    {
        public const string ResultPath = "Temp/flusi-tests.txt";
        public const string FailPath = "Temp/flusi-tests-failures.txt";

        static FlusiTestRunner()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new FlusiTestRunner());
        }

        public static void RunEditMode() => Run(TestMode.EditMode);
        public static void RunPlayMode() => Run(TestMode.PlayMode);

        private static void Run(TestMode mode)
        {
            File.WriteAllText(ResultPath, "RUNNING " + mode);
            File.WriteAllText(FailPath, "");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = mode }));
        }

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"STATUS {result.TestStatus}");
            sb.AppendLine($"passed={result.PassCount} failed={result.FailCount} " +
                          $"skipped={result.SkipCount} inconclusive={result.InconclusiveCount}");
            File.WriteAllText(ResultPath, sb.ToString());
            Debug.Log($"[FlusiTestRunner] {result.TestStatus} " +
                      $"passed={result.PassCount} failed={result.FailCount}");
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
                File.AppendAllText(FailPath, $"{result.FullName}\n  {result.Message}\n");
        }
    }
}
