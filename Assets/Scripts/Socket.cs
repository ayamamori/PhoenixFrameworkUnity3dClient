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
    public int ReconnectAfterMs = 10000;// [ms] //TODO: set random number like: function(tries){return [1000, 2000, 5000, 10000][tries - 1]
    public int LongPollerTimeout = 20000;
    public bool SkipHeartbeat = false;

    private WebSocket Conn;
    private List<Channel> Channels = new List<Channel>();
    private List<Action> SendBuffer = new List<Action>();
    private List<Action> OnOpenCallbacks;
    private List<Action<CloseEventArgs>> OnCloseCallbacks;
    private List<Action<ErrorEventArgs>> OnErrorCallbacks;
    private List<Action<MessageEventArgs>> OnMessageCallbacks;
    private int Ref = 0;



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
        OnOpenCallbacks = new List<Action>();
        OnCloseCallbacks = new List<Action<CloseEventArgs>>();
        OnErrorCallbacks = new List<Action<ErrorEventArgs>>();
        OnMessageCallbacks = new List<Action<MessageEventArgs>>();
    }

    void DisConnect(Action callback, ushort code = (ushort)CloseStatusCode.NoStatus, string reason=""){
        //Closing status code: https://triple-underscore.github.io/RFC6455-ja.html#section-7.4
        if(Conn !=null){
            Conn.OnClose -= OnConnClose;
			if(code!=(ushort)CloseStatusCode.NoStatus) {
                Conn.Close(code, reason);
            }else{
                Conn.Close();
            }
            Conn = null;
        }
        if(callback!=null)callback();
    }

    void Connect(){
        if(Conn !=null) return;

        Conn = new WebSocket(EndPoint);
        Conn.WaitTime = TimeSpan.FromMilliseconds(LongPollerTimeout);
        Conn.OnOpen += OnConnOpen;
        Conn.OnClose += OnConnClose;
        Conn.OnError += OnConnError;
        Conn.OnMessage += OnConnMessage;
        Conn.Connect();
    }

    Socket OnOpen(Action callback){
        OnOpenCallbacks.Add(callback);
        return this;
    }
    Socket OnClose(Action<CloseEventArgs> callback){
        OnCloseCallbacks.Add(callback);
        return this;
    }
    Socket OnError(Action<ErrorEventArgs> callback){
        OnErrorCallbacks.Add(callback);
        return this;
    }
    Socket OnMessage(Action<MessageEventArgs> callback){
        OnMessageCallbacks.Add(callback);
        return this;
    }

    void OnConnOpen(object sender, EventArgs e)
    {
        Debug.Log("Connected to "+EndPoint);
        FlushSendBuffer();
        if(!SkipHeartbeat){
            StartCoroutine(HeartbeatLoopTimer());
        }
        foreach (var callback in OnOpenCallbacks) {
            callback();
        }
    }

    void OnConnClose(object sender, CloseEventArgs e){
        Debug.Log("Connection closed");
        TriggerChanError();
        DisConnect(Connect,e.Code);//Reconnect
        foreach (var callback in OnCloseCallbacks) {
            callback(e);
        }
    }

    void OnConnError(object sender, ErrorEventArgs e){
        Debug.Log(e.Message);
        TriggerChanError();
        foreach (var callback in OnErrorCallbacks) {
            callback(e);
        }
    }


    void TriggerChanError(){
        foreach (var chan in Channels){
            chan.Trigger(Channel.CHANNEL_EVENTS.ERROR);
        }
    }

    WebSocketState ConnectionState(){
        if(Conn == null) return WebSocketState.Closed;
        return Conn.ReadyState;
    }

    public bool IsConnected(){
        return ConnectionState() == WebSocketState.Open;
    }

    public void Remove(Channel channel){
        Channels = Channels.Where(c => !c.IsMember(channel.Topic)).ToList();
    }

    private Channel CreateChannel(string topic, PayloadReq payload){
        Channel channel = Channel.getInstance(topic,this,payload);
        Channels.Add(channel);
        return channel;
    }

    public void Push(Message<PayloadReq> message){
        var jsonMessage = JsonUtility.ToJson(message);
        Action callback = () => Conn.Send(JsonUtility.ToJson(jsonMessage));
        Debug.Log("Push message: "+jsonMessage);
        if(IsConnected()){
            callback();
        }else{
            SendBuffer.Add(callback);
        }
    }

    private string MakeRef(){
        int newRef = Ref+1;
        if(newRef==int.MaxValue) {
            Ref = 0;
        } else {
            Ref = newRef;
        }
        return Ref.ToString();
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
        if(IsConnected()&& SendBuffer.Count>0){
            SendBuffer.ForEach(callback => callback());
            SendBuffer = new List<Action>();
        }
    }


    private void OnConnMessage(object sender, MessageEventArgs e){
        Message<PayloadResp> msg = JsonUtility.FromJson<Message<PayloadResp>>(e.Data);
        Debug.Log(msg);
        Channels.Where(c => c.IsMember(msg.Topic)).ToList().ForEach(c => c.Trigger(msg.Event, msg.Payload,msg.Ref));

        foreach (var callback in OnMessageCallbacks) {
            callback(e);
        }
    }


}
