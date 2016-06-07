using System;

[Serializable]
public class PayloadResp :Payload{
    public string Status;
    public string Response;

    public PayloadResp(string json = ""){
        if(json==null||"".Equals(json)) return;
    }
    //TODO: Implementation

}
