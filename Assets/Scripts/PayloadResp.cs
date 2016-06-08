using System;
using UnityEngine;

[Serializable]
public class PayloadResp :Payload{
    public string status;
    public string response;

    public PayloadResp(string status ="", string response =""){
        status =  status;
        response = response;
    }

}
