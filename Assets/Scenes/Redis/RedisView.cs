using StackExchange.Redis;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class RedisView : MonoBehaviour {

    public const string KEY_TEST = "testkey";

    [SerializeField]
    protected string server = "127.0.0.1";

    protected Rect windowSize = new Rect(10, 10, 300, 300);
    protected string valueForSet = null;
    protected string valueForGet = null;
    protected RedisService redis;
    protected System.Diagnostics.Stopwatch guiStopwatch;
    protected long guiElapsed;
    protected Coroutine procBenchmark;

    #region unity
    private void OnEnable() {
        redis = new RedisService();
        guiStopwatch = new System.Diagnostics.Stopwatch();
    }
    private void OnDisable() {
        redis?.Dispose();
        redis = null;
    }
    private void OnGUI() {
        windowSize = GUILayout.Window(GetInstanceID(), windowSize, OnWindow, name);
    }
    private void Update() {
        if (redis.CurrConnectionStat == RedisService.ConnectionStat.None) {
            var config = ConfigurationOptions.Parse(server);
            redis.Connect(config);
        }
    }
    #endregion

    #region member
    protected void OnWindow(int id) {
        GUI.enabled = redis.IsConnected && procBenchmark == null;

        using (new GUILayout.VerticalScope()) {
            using (new GUILayout.HorizontalScope()) {
                GUILayout.Label("Value for set:");
                valueForSet = GUILayout.TextField(valueForSet);
            }
            if (GUILayout.Button("Set")) {
                valueForGet = "";
                StartBenchmark();
                redis?.GetDB().StringSet(KEY_TEST, valueForSet);
                guiElapsed = StopBenchmark();
            }
            using (new GUILayout.HorizontalScope()) {
                GUILayout.Label($"Value for get: {valueForGet}");
            }
            if (GUILayout.Button("Get")) {
                StartBenchmark();
                valueForGet = redis?.GetDB().StringGet(KEY_TEST);
                guiElapsed = StopBenchmark();
            }
            GUILayout.Label($"Elapsed: {guiElapsed:f1}ms");

            if (GUILayout.Button("Benchmark")) {
                procBenchmark = StartCoroutine(CoBenchmark());
            }
        }

        GUI.DragWindow();
    }
    protected void StartBenchmark() {
        guiStopwatch.Restart();
    }
    protected long StopBenchmark() {
        guiStopwatch.Stop();
        return guiStopwatch.ElapsedMilliseconds;
    }

    protected IEnumerator CoBenchmark() {
        yield return null;

        var log = new StringBuilder();
        var bulkIter = 1000;
        var minElapsedMilli = 1000;
        var key = "benchKey";
        var val = "benchVal";
        var keySubs = "benchSubsKey";
        var db = redis.GetDB();

        var counter = 0;
        db.StringSet(key, val);
        StartBenchmark();
        for (counter = 0; guiStopwatch.ElapsedMilliseconds < minElapsedMilli; ) {
            for (var i = 0; i < bulkIter; i++) {
                db.StringGet(key);
                counter++;
            }
        }
        var elapsed = (float)StopBenchmark() / counter;
        log.AppendLine($"Latency of get: {elapsed:f2}ms");
        yield return null;

        var subsc = redis.GetSub();
        var channel = subsc.Subscribe(keySubs);
        counter = 0;
        channel.OnMessage(cm => {
            counter++;
            if ((counter % bulkIter) == 0
                && guiStopwatch.ElapsedMilliseconds >= minElapsedMilli) {
                guiStopwatch.Stop();
                return;
            }
            subsc.Publish(keySubs, val);
        });
        StartBenchmark();
        subsc.Publish(keySubs, val);
        while (guiStopwatch.IsRunning)
            yield return null;
        elapsed = (float)guiStopwatch.ElapsedMilliseconds / counter;
        log.AppendLine($"Latency of pub/sub: {elapsed:f2}ms");

        Debug.Log(log.ToString());
        procBenchmark = null;
    }
    #endregion
}
