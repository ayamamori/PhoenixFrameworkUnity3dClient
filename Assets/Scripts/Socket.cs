using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WebSocketSharp;

public class Socket : MonoBehaviour{

    public string EndPoint;

    public int Timeout = DEFAULT_TIMEOUT;
    public int HeartbeatIntervalMs = 30000;//[ms]
    public int[] ReconnectAfterMs = new int[] {1000, 2000, 5000, 10000};// [ms]
    public int LongPollerTimeout = 20000;//[ms]
    public bool SkipHeartbeat = false;

    private WebSocket conn;
    private List<Channel> channels = new List<Channel>();
    private List<Action> sendBuffer = new List<Action>();
    private List<Action> onOpenCallbacks;
    private List<Action<CloseEventArgs>> onCloseCallbacks;
    private List<Action<ErrorEventArgs>> onErrorCallbacks;
    private List<Action<MessageEventArgs>> onMessageCallbacks;
    private int socket_ref = 0;

    private int reconnectCount;


    public static int DEFAULT_TIMEOUT = 10000;//[ms]

    // Initializes the Socket
    //
    // endPoint - The string WebSocket endpoint, ie, "ws://example.com/ws",
    //                                               "wss://example.com"
    //                                               "/ws" (inherited host & protocol)
    // opts - Optional configuration
    //   transport - The Websocket Transport, for example WebSocket or Phoenix.LongPoll.
    //               Defaults to WebSocket with automatic LongPoll fallback.
    //   timeout - The default timeout in milliseconds to trigger push timeouts.
    //             Defaults `DEFAULT_TIMEOUT`
    //   heartbeatIntervalMs - The millisec interval to send a heartbeat message
    //   reconnectAfterMs - The optional function that returns the millsec
    //                      reconnect interval. Defaults to stepped backoff of:
    //
    //     function(tries){
    //       return [1000, 5000, 10000][tries - 1] || 10000
    //     }
    //
    //   logger - The optional function for specialized logging, ie:
    //     `logger: (kind, msg, data) => { console.log(`${kind}: ${msg}`, data) }
    //
    //   longpollerTimeout - The maximum timeout of a long poll AJAX request.
    //                        Defaults to 20s (double the server long poll timer).
    //
    //   params - The optional params to pass when connecting
    //
    // For IE8 support use an ES5-shim (https://github.com/es-shims/es5-shim)
    //

    void Awake(){
        onOpenCallbacks = new List<Action>();
        onCloseCallbacks = new List<Action<CloseEventArgs>>();
        onErrorCallbacks = new List<Action<ErrorEventArgs>>();
        onMessageCallbacks = new List<Action<MessageEventArgs>>();
    }

    public void DisConnect(Action callback, ushort code = (ushort)CloseStatusCode.NoStatus, string reason=""){
        //Closing status code: https://triple-underscore.github.io/RFC6455-ja.html#section-7.4
        if(conn !=null){
            conn.OnClose -= OnConnClose;
			if(code!=(ushort)CloseStatusCode.NoStatus) {
                conn.Close(code, reason);
            }else{
                conn.Close();
            }
            conn = null;
        }
        if(callback!=null)callback();
    }

    public void Connect() {
        lock(this) {
            if (conn != null) return;
            conn = new WebSocket(EndPoint);
            conn.WaitTime = TimeSpan.FromSeconds(10);
            conn.OnOpen += OnConnOpen;
            conn.OnClose += OnConnClose;
            conn.OnError += OnConnError;
            conn.OnMessage += OnConnMessage;
            conn.Connect();
        }
    }

    Socket OnOpen(Action callback){
        onOpenCallbacks.Add(callback);
        return this;
    }
    Socket OnClose(Action<CloseEventArgs> callback){
        onCloseCallbacks.Add(callback);
        return this;
    }
    Socket OnError(Action<ErrorEventArgs> callback){
        onErrorCallbacks.Add(callback);
        return this;
    }
    Socket OnMessage(Action<MessageEventArgs> callback){
        onMessageCallbacks.Add(callback);
        return this;
    }

    void OnConnOpen(object sender, EventArgs e)
    {
        Debug.Log("Connected to "+EndPoint);
        FlushSendBuffer();
        reconnectCount =0;
        if(!SkipHeartbeat){
            StartCoroutine(HeartbeatLoopTimer());
        }
        foreach (var callback in onOpenCallbacks) {
            callback();
        }
    }

    void OnConnClose(object sender, CloseEventArgs e){
        Debug.Log("Connection closed");
        TriggerChanError();
        StartCoroutine(DisConnectTimer(Connect,e.Code));//Reconnect
        foreach (var callback in onCloseCallbacks) {
            callback(e);
        }
    }

    void OnConnError(object sender, ErrorEventArgs e){
        Debug.Log("Connection error: "+e.Message);
        TriggerChanError();
        foreach (var callback in onErrorCallbacks) {
            callback(e);
        }
    }

    IEnumerator DisConnectTimer(Action callback, ushort code){
        yield return new WaitForSeconds(ReconnectAfterMs[reconnectCount]/1000.0f);
        reconnectCount++;
        if(reconnectCount >=ReconnectAfterMs.Length) reconnectCount--;

        Debug.Log("Connection retry");
        DisConnect(callback,code);
    }

    void TriggerChanError(){
        foreach (var chan in channels){
            chan.Trigger(Channel.CHANNEL_EVENTS.ERROR);
        }
    }

    WebSocketState ConnectionState(){
        if(conn == null) return WebSocketState.Closed;
        return conn.ReadyState;
    }

    public bool IsConnected(){
        return ConnectionState() == WebSocketState.Open;
    }

    public void Remove(Channel channel){
        channels = channels.Where(c => !c.IsMember(channel.Topic)).ToList();
    }

    public void CreateChannel(){
        CreateChannel("rooms:lobby").Join();
    }
    public Channel CreateChannel(string topic) {
        return CreateChannel(topic, new PayloadReq());
    }

    public Channel CreateChannel(string topic, PayloadReq payload){
        Channel channel = Channel.GetInstance(topic,this,payload);
        channels.Add(channel);
        return channel;
    }

    public void Push(Message<PayloadReq> message){
        var jsonMessage = JsonUtility.ToJson(message);
        Action callback = () => conn.Send(jsonMessage);
        if(IsConnected()){
            callback();
        }else{
            sendBuffer.Add(callback);
        }
    }

    public string MakeRef(){
        lock(this) {
            int newRef = socket_ref + 1;
            if (newRef == int.MaxValue) {
                socket_ref = 0;
            } else {
                socket_ref = newRef;
            }
        }
        return socket_ref.ToString();
    }

    private IEnumerator HeartbeatLoopTimer(){
        while(IsConnected()) {
            yield return new WaitForSeconds(HeartbeatIntervalMs / 1000.0f);
            SendHeartbeat();
        }
    }

    private void SendHeartbeat(){
        Push(new Message<PayloadReq>("Phoenix","heartbeart",new PayloadReq(""),MakeRef()));
    }

    private void FlushSendBuffer(){
        if(IsConnected()&& sendBuffer.Count>0){
            sendBuffer.ForEach(callback => callback());
            sendBuffer = new List<Action>();
        }
    }


    void OnConnMessage(object sender, MessageEventArgs e){
        Message<PayloadResp> msg = JsonUtility.FromJson<Message<PayloadResp>>(e.Data);
        channels.Where(c => c.IsMember(msg.topic)).ToList().ForEach(c => c.Trigger(msg.@event, msg.payload,msg.@ref));

        foreach (var callback in onMessageCallbacks) {
            callback(e);
        }
    }


}
