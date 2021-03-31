using StackExchange.Redis;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class RedisService : System.IDisposable {

    public enum ConnectionStat { None = 0, Connecting, Connected }

    protected ConnectionStat connStat;
    protected Task taskConnection = null;

    #region interfaces

    #region IDisposable
    public void Dispose() {
        Disconnect();
    }
    #endregion

    public ConnectionStat CurrConnectionStat => connStat;
    public ConnectionMultiplexer CurrRedis { get; protected set; }
    public bool IsConnected => connStat == ConnectionStat.Connected && CurrRedis.IsConnected;

    public RedisService Connect(string config) {
        return Connect(ConfigurationOptions.Parse(config));
    }
    public RedisService Connect(ConfigurationOptions config) {
        if (taskConnection != null) {
            taskConnection.Dispose();
        }
        Disconnect();

        connStat = ConnectionStat.Connecting;
        taskConnection = ConnectionMultiplexer.ConnectAsync(config)
            .ContinueWith(v => {
                try {
                    CurrRedis = null;

                    if (v.IsFaulted)
                        Debug.LogWarning(v.Exception);
                    else
                        CurrRedis = v.Result;
                    connStat = (CurrRedis != null) ? ConnectionStat.Connected : ConnectionStat.None;
                } catch (System.Exception e) {
                    Debug.LogWarning(e);
                }
            });
        return this;
    }
    public RedisService Disconnect() {
        if (CurrRedis != null) {
            CurrRedis.Dispose();
            CurrRedis = null;
        }
        connStat = ConnectionStat.None;
        return this;
    }
    public IDatabase GetDB() {
        return CurrRedis.GetDatabase();
    }
    public ISubscriber GetSub() {
        return CurrRedis.GetSubscriber();
    }
    #endregion
}
