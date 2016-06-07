using System;

[Serializable]
public class PayloadReq : Payload{
    public string Content;

    public PayloadReq(string _content=""){
        Content = _content;
    }
}
