using System;
using System.Text;

[Serializable]
public class PayloadResp :Payload{
    public string status;
    public Response response;
    public string body;


    public PayloadResp(){}
    public PayloadResp(string _status){
        status = _status;
    }

    public override string ToString(){
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append("status: " + status+",");
        sb.Append("response: " + response+",");
        sb.Append("body: " + body);
        sb.Append("}");
        return sb.ToString();
    }

}
