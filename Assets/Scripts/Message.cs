using System;
using UnityEngine;

[Serializable]
public class Message<PL> where PL :Payload {
	public readonly string Topic;
	public readonly string Event;
	public readonly PL Payload;
	public readonly int Ref;

	public Message(string _topic, string _event, PL _payload, int _ref) {
        Topic = _topic;
        Event = _event;
        Payload = _payload;
        Ref = _ref;
    }

}
