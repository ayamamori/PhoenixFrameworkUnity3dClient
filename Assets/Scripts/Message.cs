using System;
using System.Text;

[Serializable]
public class Message<PL> where PL :Payload {
	public string topic;
	public string @event;
	public PL payload;
	public string @ref;

	public Message(string _topic, string _event, PL _payload, string _ref) {
        topic = _topic;
        @event = _event;
        payload = _payload;
        @ref = _ref;
    }

    public override string ToString(){
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append("topic: " + topic+",");
        sb.Append("event: " + @event+",");
        sb.Append("payload: " + payload+",");
        sb.Append("ref: " + @ref);
        sb.Append("}");
		return sb.ToString ();
    }

}
