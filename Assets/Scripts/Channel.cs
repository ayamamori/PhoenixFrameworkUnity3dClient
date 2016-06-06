using System;
using System.Collections.Generic;
using UnityEngine;

public class Channel : MonoBehaviour{
    CHANNEL_STATES State;
    string Topic;
    Socket Socket;
    Payload Payload;
    int Timeout;
    bool JoinedOnce;
    Push JoinPush;
    Dictionary<CHANNEL_EVENTS,Action> Bindings = new Dictionary<CHANNEL_EVENTS,Action>();
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

    static Channel getInstance(string _topic, Socket _socket, Payload _payload = new Payload()){
        Channel channel = _socket.gameObject.AddComponent<Channel>();
        channel.Config(_topic,_socket,_payload);
        return channel;

    }
    void Config (string _topic, Socket _socket, Payload _payload = new Payload()){
        State = CHANNEL_STATES.CLOSED;
        Topic = _topic;
        Socket = _socket;
        Payload = _payload;
        Timeout = _socket.Timeout;
        JoinedOnce = false;
        JoinPush = Push.getInstance(this,CHANNEL_EVENTS.JOIN,Payload,Timeout);
        //TODO Rejoin Timer

        JoinPush.Receive("ok",() => {
            State = CHANNEL_STATES.JOINED;
            //TODO Rejoin Timer reset
            PushBuffer.ForEach(events => events.Send());
        });

        JoinPush.Receive("timeout",() => {
            if(State!=CHANNEL_STATES.JOINING) return;
            Debug.Log("Timeout on topic: "+Topic+" "+JoinPush.Timeout);
            State = CHANNEL_STATES.ERRORED;
            //TODO Rejoin Timer set
        });

        OnClose(() => {
            Debug.Log("Close channel: "+Topic);
            State = CHANNEL_STATES.CLOSED;
            Socket.Remove(this);
        });

        OnError(reason => {
            Debug.Log("Error on topic: "+Topic+ " reason: "+reason);
            State = CHANNEL_STATES.ERRORED;
            //TODO Rejoin Timer set
        });
        OnReply((payloadResp, refResp) => {
            Trigger(ReplyEventName(refResp),payloadResp);
            //FIXME: refResp is really string?
        });


    }

    void OnClose(Action callback){
        //On(CHANNEL_EVENTS.CLOSE,callback);
        //TODO
    }
    void OnError(Action<string> callback){
        //On(CHANNEL_EVENTS.ERROR, callback);
        //TODO
    }

    void OnReply(Action<Payload,string> callback){
        //On(CHANNEL_EVENTS.REPLY, callback);
        //TODO
    }
    /*
    //TODO Remove it
    void On(CHANNEL_EVENTS _event, Action callback){
        Bindings.Add(_event,callback);
    }
    */

    void Trigger(string _event, Payload _payload = null, int _ref = -1){
        //TODO Method Stub
    }

    public bool IsMember(string topic) {
        //TODO Method Stub
        return false;
    }


    string ReplyEventName(string refResp){
        //TODO Method Stub
    }

}