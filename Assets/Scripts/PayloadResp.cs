using System;
using UnityEngine;

[Serializable]
public class PayloadResp :Payload{
    public string Status;
    public string Response;

    public PayloadResp(string status ="", string response =""){
        Status =  status;
        Response = response;
    }

}
