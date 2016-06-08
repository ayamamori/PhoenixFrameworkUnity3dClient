using System;
using System.Text;

[Serializable]
public class PayloadResp :Payload{
    public string status;
    public string response;

    public PayloadResp(string _status ="", string _response =""){
        status = _status;
        response = _response;
    }

    public override string ToString(){
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append("status: " + status+",");
        sb.Append("response: " + response);
        sb.Append("}");
        return sb.ToString();
    }

}
