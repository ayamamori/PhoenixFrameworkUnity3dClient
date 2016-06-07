using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Channel : MonoBehaviour{
    public Socket Socket;
    public string Topic;

    CHANNEL_STATES State;
    PayloadReq PayloadReq;
    int Timeout;//[ms]
    bool JoinedOnce;
    Push JoinPush;
    Dictionary<string,Action<PayloadResp,string>> Bindings = new Dictionary<string, Action<PayloadResp, string>>();
    List<Push> PushBuffer = new List<Push>();



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

    public static Channel getInstance(string _topic, Socket _socket){
		return getInstance(_topic, _socket, new PayloadReq());
	}

    public static Channel getInstance(string _topic, Socket _socket, PayloadReq _payload){
        Channel channel = _socket.gameObject.AddComponent<Channel>();
        channel.Config(_topic,_socket,_payload);
        return channel;

    }
    private void Config (string _topic, Socket _socket, PayloadReq _payload){
        State = CHANNEL_STATES.CLOSED;
        Topic = _topic;
        Socket = _socket;
        PayloadReq = _payload;
        Timeout = _socket.Timeout;
        JoinedOnce = false;
        JoinPush = Push.getInstance(this,CHANNEL_EVENTS.JOIN, PayloadReq,Timeout);

        JoinPush.Receive("ok",(nop) => {
            State = CHANNEL_STATES.JOINED;
            PushBuffer.ForEach(events => events.Send());
        });

        JoinPush.Receive("timeout",(nop) => {
            if(State!=CHANNEL_STATES.JOINING) return;
            Debug.Log("Timeout on topic: "+Topic+" "+JoinPush.Timeout);
            State = CHANNEL_STATES.ERRORED;
            StartCoroutine(RejoinLoopTimer());
        });

        OnClose((payloadResp, refResp) => {
            Debug.Log("Close channel: "+Topic);
            State = CHANNEL_STATES.CLOSED;
            Socket.Remove(this);
        });

        OnError((reason, refResp) => {
            Debug.Log("Error on topic: "+Topic+ " reason: "+reason);
            State = CHANNEL_STATES.ERRORED;
            StartCoroutine(RejoinLoopTimer());
        });
        OnReply((payloadResp, refResp) => {
            Trigger(ReplyEventName(refResp),payloadResp);
        });


    }

    IEnumerator RejoinLoopTimer() {
        while (Socket.IsConnected()) {
            Rejoin(Timeout);
            yield return new WaitForSeconds(Socket.ReconnectAfterMs / 1000.0f);
        }

    }

	public Push Join (){
		return Join (Timeout);
	}

	public Push Join (int timeout){
        if(JoinedOnce){
            throw new InvalidOperationException("tried to join multiple times. 'join' can only be called a single time per channel instance");
        }else{
            JoinedOnce = true;
        }
        Rejoin(timeout);
        return JoinPush;
    }

    void Rejoin(int timeout){
        State = CHANNEL_STATES.JOINING;
        JoinPush.Resend(timeout);
    }


    void OnClose(Action<PayloadResp,string> callback){
        On(CHANNEL_EVENTS.CLOSE,callback);
    }
    void OnError(Action<PayloadResp,string> callback){
        On(CHANNEL_EVENTS.ERROR, callback);
    }

    void OnReply(Action<PayloadResp,string> callback){
        On(CHANNEL_EVENTS.REPLY, callback);
    }
    public void On(string _event, Action<PayloadResp,string> callback){
        Bindings.Add(_event,callback);
    }

    public void Off(string _event){
        Bindings = Bindings.Where(kvp => !kvp.Key.Equals(_event)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    bool CanPush(){
        return Socket.IsConnected() && State == CHANNEL_STATES.JOINED;
    }

    Push Push(string _event, PayloadReq _payloadReq){
        return Push(_event, _payloadReq, Timeout);
    }

    Push Push(string _event, PayloadReq _payloadReq, int timeout){
        if(!JoinedOnce){
            throw new InvalidOperationException("tried to push '"+_event+"' to '"+Topic+"' before joining. Use Channel.Join() before pushing events");
        }
        Push pushEvent = Push.getInstance(this, _event, _payloadReq, timeout);
        if(CanPush()){
            pushEvent.Send();
        }else{
            pushEvent.SetResponseListener();
            PushBuffer.Add(pushEvent);
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
        return Leave(Timeout);
    }
    Push Leave(int timeout){
        Action<string> onClose = (nop) =>  {
            Debug.Log("leave topic: "+Topic);
            Trigger(CHANNEL_EVENTS.CLOSE,new PayloadResp("leave"));
        };
        Push leavePush = Push.getInstance(this,CHANNEL_EVENTS.LEAVE, new PayloadReq(),timeout);
        leavePush.Receive("ok", onClose)
            .Receive("timeout", onClose);
        leavePush.Send();
        if(!CanPush()){
            leavePush.Trigger("ok",new PayloadResp());
        }
        return leavePush;
    }

    void OnMessage(string _event, PayloadResp payloadResp, string _ref){ }

    public bool IsMember(string topic) {
        return this.Topic == topic;
    }

    public void Trigger(string _event){
        Trigger(_event, new PayloadResp());
    }

	public void Trigger(string _event, PayloadResp _payload,  string _ref = null){
        OnMessage(_event,_payload, _ref);
        Bindings.Where(kvp => kvp.Key.Equals(_event))
                .Select(kvp => kvp.Value)
                .ToList()
                .ForEach(bind => bind.Invoke(_payload,_ref));
    }


    string ReplyEventName(string refResp){
		return "chan_reply_"+refResp;
    }

}
