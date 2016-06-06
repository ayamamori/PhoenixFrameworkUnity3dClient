using System;
using System.Collections;
using UnityEngine;

public class Push: MonoBehaviour {

    Channel Channel;
    string Event;
    Payload Payload;
    int Timeout;
    Payload ReceivedResp = null;
    bool Sent = false;
    int Ref;//TODO

    // Initializes the Push
    //
    // channel - The Channel
    // event - The event, for example `"phx_join"`
    // payload - The payload, for example `{user_id: 123}`
    // timeout - The push timeout in milliseconds
    //
    static Push getInstance(Channel _channel, string _event, Payload _payload, int _timeout){
        Push push = _channel.gameObject.AddComponent<Push>();
        push.Channel = _channel;
        push.Event = _event;
        push.Payload = _payload;
        push.Timeout = _timeout;
        return push;
    }
    void Resend(int _timeout){
        this.Timeout = _timeout;
        CancelRefEvent();
        Sent = false;
        Send();
    }
    void Send(){
        if(HasReceived("timeout"))
            return;
        StartCoroutine(StartTimeout());
        Sent = true;
        Channel.Socket.Push(new Message(Channel.Topic,Event,Payload,Ref));
    }

    void Receive(string status,Action callback){
        //TODO Stub
    }

    bool HasReceived(string _status){
        return ReceivedResp !=null && ReceivedResp.Status == _status;
    }

    IEnumerator StartTimeout(){

    }
    void CancelRefEvent(){
        //TODO Stub
        /*
        if(RefEvent==null)return;
        this.Channel.Off(RefEvent);
         */
    }

}
