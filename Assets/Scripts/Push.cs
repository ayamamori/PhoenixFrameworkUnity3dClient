using System;
using System.Collections;
using UnityEngine;

public class Push: MonoBehaviour {

    public int Timeout;

    Channel Channel;
    string Event;
    PayloadReq PayloadReq;
    PayloadResp PayloadResp = null;
    bool Sent = false;
    string Ref;//TODO

    // Initializes the Push
    //
    // channel - The Channel
    // event - The event, for example `"phx_join"`
    // payload - The payload, for example `{user_id: 123}`
    // timeout - The push timeout in milliseconds
    //
    public static Push getInstance(Channel _channel, string _event, PayloadReq _payload, int _timeout){
        Push push = _channel.gameObject.AddComponent<Push>();
        push.Channel = _channel;
        push.Event = _event;
        push.PayloadReq = _payload;
        push.Timeout = _timeout;
        return push;
    }
    void Resend(int _timeout){
        this.Timeout = _timeout;
        CancelRefEvent();
        Sent = false;
        Send();
    }
    public void Send(){
        if(HasReceived("timeout"))
            return;
        StartCoroutine(StartTimeout());
        Sent = true;
        Channel.Socket.Push(new Message<PayloadReq>(Channel.Topic, Event, PayloadReq, Ref));
    }

    public void Receive(string status,Action callback){
        //TODO Stub
    }

    bool HasReceived(string _status){
        return PayloadResp !=null && PayloadResp.Status == _status;
    }

    IEnumerator StartTimeout(){
		yield return null;

    }
    void CancelRefEvent(){
        //TODO Stub
        /*
        if(RefEvent==null)return;
        this.Channel.Off(RefEvent);
         */
    }

}
