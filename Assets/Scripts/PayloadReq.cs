using System;

[Serializable]
public class PayloadReq : Payload{
    public string body;

    public PayloadReq(string _body =""){
        body = _body;
    }

    public override string ToString(){
        return "{body: "+ body +"}";
    }
}
