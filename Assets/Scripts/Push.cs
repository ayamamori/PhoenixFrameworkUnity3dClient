using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Push: MonoBehaviour {

    public int Timeout;//[ms]

    Channel channel;
    string eventName;
    PayloadReq payloadReq;
    PayloadResp payloadResp = null;
    bool sent = false;
    string push_ref;
    string refEvent;
    PayloadResp receivedResp;
    Dictionary<string,Action<string>> recHooks = new Dictionary<string, Action<string>>();

    // Initializes the Push
    //
    // channel - The Channel
    // event - The event, for example `"phx_join"`
    // payload - The payload, for example `{user_id: 123}`
    // timeout - The push timeout in milliseconds
    //
    public static Push GetInstance(Channel _channel, string _event, PayloadReq _payload, int _timeout){
        Push push = _channel.gameObject.AddComponent<Push>();
        push.channel = _channel;
        push.eventName = _event;
        push.payloadReq = _payload;
        push.Timeout = _timeout;
        return push;
    }
    public void Resend(int _timeout){
        this.Timeout = _timeout;
        CancelRefEvent();
        sent = false;
        Send();
    }
    public void Send(){
        if(HasReceived("timeout"))
            return;
        SetResponseListener();
        sent = true;
        channel.Socket.Push(new Message<PayloadReq>(channel.Topic, eventName, payloadReq, push_ref));
        StartCoroutine(StartTimeout());
    }

    public Push Receive(string status,Action<string> callback){
        if(HasReceived(status)){
            callback(receivedResp.response);
        }
        recHooks.Add(status,callback);
        return this;
    }

    void MatchReceive(PayloadResp msg){
        recHooks.Where(kvp => kvp.Key.Equals(msg.status))
                .Select(kvp => kvp.Value)
                .ToList()
                .ForEach(callback => callback(msg.response));
    }

    void CancelRefEvent(){
        if(refEvent ==null) return;
        channel.Off(refEvent);
    }

    public void SetResponseListener(){
        push_ref = channel.Socket.MakeRef();
        refEvent = channel.ReplyEventName(push_ref);
        channel.On(refEvent, (payloadResp,refResp) => {
            CancelRefEvent();
            StopCoroutine(StartTimeout());//FIXME
            receivedResp = payloadResp;
            MatchReceive(payloadResp);
        });
    }
    IEnumerator StartTimeout(){
        yield return new WaitForSeconds(Timeout/1000.0f);
        Trigger("timeout", new PayloadResp());
    }

    bool HasReceived(string _status){
        return payloadResp !=null && payloadResp.status == _status;
    }

    public void Trigger(string _status, PayloadResp payloadResp) {
        payloadResp.status = _status;
        channel.Trigger(refEvent,payloadResp);
    }

}
