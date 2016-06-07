using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Push: MonoBehaviour {

    public int Timeout;//[ms]

    Channel Channel;
    string Event;
    PayloadReq PayloadReq;
    PayloadResp PayloadResp = null;
    bool Sent = false;
    string Ref;//TODO
    string RefEvent; //TODO
    PayloadResp ReceivedResp;//TODO
    Dictionary<string,Action<string>> RecHooks = new Dictionary<string, Action<string>>();

    bool WaitingResponse;

    // Initializes the Push
    //
    // channel - The Channel
    // event - The event, for example `"phx_join"`
    // payload - The payload, for example `{user_id: 123}`
    // timeout - The push timeout in milliseconds
    //
    public static Push GetInstance(Channel _channel, string _event, PayloadReq _payload, int _timeout){
        Push push = _channel.gameObject.AddComponent<Push>();
        push.Channel = _channel;
        push.Event = _event;
        push.PayloadReq = _payload;
        push.Timeout = _timeout;
        return push;
    }
    public void Resend(int _timeout){
        this.Timeout = _timeout;
        CancelRefEvent();
        Sent = false;
        Send();
    }
    public void Send(){
        if(HasReceived("timeout"))
            return;
        SetResponseListener();
        Sent = true;
        Channel.Socket.Push(new Message<PayloadReq>(Channel.Topic, Event, PayloadReq, Ref));
        StartCoroutine(StartTimeout());
    }

    public Push Receive(string status,Action<string> callback){
        if(HasReceived(status)){
            callback(ReceivedResp.Response);
        }
        RecHooks.Add(status,callback);
        return this;
    }

    void MatchReceive(PayloadResp msg){
        RecHooks.Where(kvp => kvp.Key.Equals(msg.Status))
                .Select(kvp => kvp.Value)
                .ToList()
                .ForEach(callback => callback(msg.Response));
    }

    void CancelRefEvent(){
        if(RefEvent==null) return;
        Channel.Off(RefEvent);
    }

    public void SetResponseListener(){
        WaitingResponse = true;
        Ref = Channel.Socket.MakeRef();
        RefEvent = Channel.ReplyEventName(Ref);
        Channel.On(RefEvent, (payloadResp,refResp) => {
            CancelRefEvent();
            WaitingResponse = false;
            ReceivedResp = payloadResp;
            MatchReceive(payloadResp);
        });
    }
    IEnumerator StartTimeout(){
        yield return new WaitForSeconds(Timeout/1000.0f);
        if(WaitingResponse) {
            Trigger("timeout", new PayloadResp());
        }
    }

    bool HasReceived(string _status){
        return PayloadResp !=null && PayloadResp.Status == _status;
    }

    public void Trigger(string _status, PayloadResp payloadResp) {
        payloadResp.Status = _status;
        Channel.Trigger(RefEvent,payloadResp);
    }

}
