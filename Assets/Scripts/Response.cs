using System;
using System.Text;

[Serializable]
public class Response {
    public string reason;
    public string body;


    public override string ToString(){
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append("reason: "+ reason+",");
        sb.Append("}");
        return sb.ToString();
    }
}
