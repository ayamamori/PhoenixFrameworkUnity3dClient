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

    public static int DEFAULT_TIMEOUT = 10000;//[ms]

    WebSocket conn;
    List<Channel> channels = new List<Channel>();
    List<Action> sendBuffer = new List<Action>();
    List<Action> onOpenCallbacks;
    List<Action<CloseEventArgs>> onCloseCallbacks;
    List<Action<ErrorEventArgs>> onErrorCallbacks;
    List<Action<MessageEventArgs>> onMessageCallbacks;
    int socket_ref = 0;




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

    public Socket OnOpen(Action callback){
        onOpenCallbacks.Add(callback);
        return this;
    }
    public Socket OnClose(Action<CloseEventArgs> callback){
        onCloseCallbacks.Add(callback);
        return this;
    }
    public Socket OnError(Action<ErrorEventArgs> callback){
        onErrorCallbacks.Add(callback);
        return this;
    }
    public Socket OnMessage(Action<MessageEventArgs> callback){
        onMessageCallbacks.Add(callback);
        return this;
    }

    void OnConnOpen(object sender, EventArgs e)
    {
        Debug.Log("Connected to "+EndPoint);
        FlushSendBuffer();
        reconnectNum =0;
        startReconnectTimer = false;
        if(!SkipHeartbeat){
            StartCoroutine(HeartbeatLoopTimer());
        }
        foreach (var callback in onOpenCallbacks) {
            callback();
        }
    }

    void OnConnClose(object sender, CloseEventArgs e){
        Debug.Log("Connection closed. code: "+e.Code+" reason: "+e.Reason);
        TriggerChanError();
        startReconnectTimer = true;
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

    int reconnectNum =0;
    bool startReconnectTimer = false;
    void Update(){
        if(startReconnectTimer){
            startReconnectTimer=false;
            StartCoroutine(ReconnectTimer());
        }
    }

    IEnumerator ReconnectTimer(){
        yield return new WaitForSeconds(ReconnectAfterMs[reconnectNum] / 1000.0f);

        reconnectNum++;
        if(reconnectNum>=ReconnectAfterMs.Length) reconnectNum=ReconnectAfterMs.Length-1;

        Debug.Log("Connection retry");
        DisConnect(Connect,(ushort)CloseStatusCode.NoStatus);
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

    public Channel CreateChannel(string topic) {
        return CreateChannel(topic, new PayloadReq());
    }

    public Channel CreateChannel(string topic, PayloadReq payload){
        Channel channel = Channel.GetInstance(topic,this,payload);
        channels.Add(channel);
        return channel;
    }

    public void Push(Message<PayloadReq> message){
        Debug.Log("Pushing message: "+message);
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

    IEnumerator HeartbeatLoopTimer(){
        while(IsConnected()) {
            yield return new WaitForSeconds((float)HeartbeatIntervalMs / 1000.0f);
            SendHeartbeat();
        }
    }

    void SendHeartbeat(){
        Push(new Message<PayloadReq>("phoenix","heartbeat",new PayloadReq(""),MakeRef()));
    }

    void FlushSendBuffer(){
        if(IsConnected()&& sendBuffer.Count>0){
            sendBuffer.ForEach(callback => callback());
            sendBuffer = new List<Action>();
        }
    }


    void OnConnMessage(object sender, MessageEventArgs e){
        Debug.Log("Received message: " +e.Data);
        Message<PayloadResp> msg = JsonUtility.FromJson<Message<PayloadResp>>(e.Data);
        //FIXME: System response (PayloadResp) and application response should be divided.
        Debug.Log("Parsed message: " +msg);
        channels.Where(c => c.IsMember(msg.topic)).ToList().ForEach(c => c.Trigger(msg.@event, msg.payload,msg.@ref));

        foreach (var callback in onMessageCallbacks) {
            callback(e);
        }
    }


}
