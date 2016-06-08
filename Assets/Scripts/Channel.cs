using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Channel : MonoBehaviour{
    public Socket Socket;
    public string Topic;

    CHANNEL_STATES state;
    PayloadReq payloadReq;
    int timeout;//[ms]
    bool joinedOnce;
    Push joinPush;
    Dictionary<string,Action<PayloadResp,string>> bindings = new Dictionary<string, Action<PayloadResp, string>>();
    List<Push> pushBuffer = new List<Push>();



    enum CHANNEL_STATES {
        CLOSED,
        ERRORED,
        JOINED,
        JOINING,
    }

    public static class CHANNEL_EVENTS {
        public static string CLOSE = "phx_close";
        public static string ERROR = "phx_error";
        public static string JOIN  =  "phx_join";
        public static string REPLY = "phx_reply";
        public static string LEAVE = "phx_leave";
    }

    public static Channel GetInstance(string _topic, Socket _socket){
		return GetInstance(_topic, _socket, new PayloadReq());
	}

    public static Channel GetInstance(string _topic, Socket _socket, PayloadReq _payload){
        Channel channel = _socket.gameObject.AddComponent<Channel>();
        channel.Config(_topic,_socket,_payload);
        return channel;

    }
    void Config (string _topic, Socket _socket, PayloadReq _payload){
        state = CHANNEL_STATES.CLOSED;

        Topic = _topic;
        Socket = _socket;
        payloadReq = _payload;
        timeout = _socket.Timeout;
        joinedOnce = false;
        joinPush = Push.GetInstance(this, CHANNEL_EVENTS.JOIN, payloadReq, timeout);

        joinPush.Receive("ok",(nop) => {
            state = CHANNEL_STATES.JOINED;
            pushBuffer.ForEach(events => events.Send());
        });

        joinPush.Receive("timeout",(nop) => {
            if(state !=CHANNEL_STATES.JOINING) return;
            Debug.Log("Timeout on topic: "+Topic+" "+ joinPush.Timeout);
            state = CHANNEL_STATES.ERRORED;
            startRejoinTimer = true;
        });

        OnClose((payloadResp, refResp) => {
            Debug.Log("Close channel: "+Topic);
            state = CHANNEL_STATES.CLOSED;
            Socket.Remove(this);
        });

        OnError((payloadResp, refResp) => {
            Debug.Log("Error on topic: "+Topic+ " reason: "+payloadResp.status);
            state = CHANNEL_STATES.ERRORED;
            startRejoinTimer = true;
        });
        OnReply((payloadResp, refResp) => {
            Trigger(ReplyEventName(refResp),payloadResp);
        });
    }

    bool startRejoinTimer = false;
    int reconnectNum =0;
    void Update(){
        if(startRejoinTimer){
            startRejoinTimer = false;
            StartCoroutine(RejoinTimer());
        }
    }

    IEnumerator RejoinTimer() {
        if (!Socket.IsConnected()) yield break;

        yield return new WaitForSeconds((float)Socket.ReconnectAfterMs[reconnectNum] / 1000.0f);
        reconnectNum++;
        if(reconnectNum>=Socket.ReconnectAfterMs.Length)reconnectNum=Socket.ReconnectAfterMs.Length-1;

        Rejoin(timeout);
    }

	public Push Join (){
		return Join (timeout);
	}

	public Push Join (int timeout){
        if(joinedOnce){
            throw new InvalidOperationException("tried to join multiple times. 'join' can only be called a single time per channel instance");
        }else{
            joinedOnce = true;
        }
        Rejoin(timeout);
        return joinPush;
    }

    void Rejoin(int timeout){
        state = CHANNEL_STATES.JOINING;
        joinPush.Resend(timeout);
    }


    public Channel OnClose(Action<PayloadResp,string> callback){
        return On(CHANNEL_EVENTS.CLOSE,callback);
    }
    public Channel OnError(Action<PayloadResp,string> callback){
        return On(CHANNEL_EVENTS.ERROR, callback);
    }

    public Channel OnReply(Action<PayloadResp,string> callback){
        return On(CHANNEL_EVENTS.REPLY, callback);
    }
    public Channel On(string _event, Action<PayloadResp,string> callback){
        bindings.Add(_event,callback);
        return this;
    }

    public Channel Off(string _event){
        bindings = bindings.Where(kvp => !kvp.Key.Equals(_event)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return this;
    }

    public bool CanPush(){
        return Socket.IsConnected() && state == CHANNEL_STATES.JOINED;
    }

    public Push PushEvent(string _event, PayloadReq _payloadReq){
        return PushEvent(_event, _payloadReq, timeout);
    }

    Push PushEvent(string _event, PayloadReq _payloadReq, int timeout){
        if(!joinedOnce){
            throw new InvalidOperationException("tried to push '"+_event+"' to '"+Topic+"' before joining. Use Channel.Join() before pushing events");
        }
        Push pushEvent = Push.GetInstance(this, _event, _payloadReq, timeout);
        if(CanPush()){
            pushEvent.Send();
        }else{
            pushEvent.SetResponseListener();
            pushBuffer.Add(pushEvent);
        }
        return pushEvent;
    }

    // Leaves the channel
    //
    // Unsubscribes from server events, and
    // instructs channel to terminate on server
    //
    // Triggers onClose() hooks
    //
    // To receive leave acknowledgements, use the a `receive`
    // hook to bind to the server ack, ie:
    //
    //     channel.leave().receive("ok", () => alert("left!") )
    //
    Push Leave(){
        return Leave(timeout);
    }
    Push Leave(int timeout){
        Action<Response> onClose = (nop) =>  {
            Debug.Log("leave topic: "+Topic);
            Trigger(CHANNEL_EVENTS.CLOSE,new PayloadResp("leave"));
        };
        Push leavePush = Push.GetInstance(this,CHANNEL_EVENTS.LEAVE, new PayloadReq(),timeout);
        leavePush.Receive("ok", onClose)
            .Receive("timeout", onClose);
        leavePush.Send();
        if(!CanPush()){
            leavePush.Trigger("ok",new PayloadResp());
        }
        return leavePush;
    }

    public virtual void OnMessage(string _event, PayloadResp payloadResp, string _ref){ }

    public bool IsMember(string topic) {
        return this.Topic == topic;
    }

    public void Trigger(string _event){
        Trigger(_event, new PayloadResp());
    }

	public void Trigger(string _event, PayloadResp _payload,  string _ref = null){
        OnMessage(_event,_payload, _ref);
        bindings.Where(kvp => kvp.Key.Equals(_event))
                .Select(kvp => kvp.Value)
                .ToList()
                .ForEach(bind => bind.Invoke(_payload,_ref));
    }


    public string ReplyEventName(string refResp){
		return "chan_reply_"+refResp;
    }



}
