using System;
using UnityEngine;

[Serializable]
public class Message :ScriptableObject{
    string Topic;
    string Event;
    Payload Payload;
    int Ref;

    public Message(string _topic, string _event, Payload _payload, int _ref) {
        Topic = _topic;
        Event = _event;
        Payload = _payload;
        Ref = _ref;
    }

    public string ToJSONString() {
        string payloadString = Payload != null ? Payload.ToString() : "";
        return '{topic: "' +Topic+'" ,event: "' +Event+ '" ,payload: "' +payloadString+ '" ,ref: "' +Ref+ '"}';
    }
}
