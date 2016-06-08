using System;

[Serializable]
public class PayloadReq : Payload{
    public string content;

    public PayloadReq(string _content=""){
        content = _content;
    }
}
