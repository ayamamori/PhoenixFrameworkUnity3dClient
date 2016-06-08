using System;
using UnityEngine;
using UnityEngine.UI;

public class ButtonHandler : MonoBehaviour {

    public Socket Socket;
    public InputField ChatMessageToSend;
    public Text ChatDisplay;

    Channel channel;

    string chatDisplayString = "";

    public void OnConnectButton(){
        if(Socket.IsConnected()) {
            Debug.Log("Connection already established");
            return;
        }
        Socket.Connect();
    }

    public void OnChannelJoinButton(){
        if(!Socket.IsConnected()) {
            Debug.Log("Connection not yet established");
            return;
        }
        channel = Socket.CreateChannel("rooms:lobby");
        channel.On("new_msg",(payloadResp,refResp) => OnNewMessage(payloadResp,refResp))
            .Join();
        Debug.Log("Channel joined");
    }

    void OnNewMessage(PayloadResp payloadResp, string refResp){
        chatDisplayString = payloadResp.body +"\n"+ chatDisplayString;
    }

    public void OnChannelLeaveButton(){
        if(!Socket.IsConnected()) {
            Debug.Log("Connection not yet established");
            return;
        }
        if(channel==null||!channel.CanPush()){
            Debug.Log("Channel not yet joined");
            return;
        }
        channel.Leave();

    }

    public void OnChatSendButton(){
        if(!Socket.IsConnected()) {
            Debug.Log("Connection not yet established");
            return;
        }
        if(channel==null||!channel.CanPush()){
            Debug.Log("Channel not yet joined");
            return;
        }
        string msg = ChatMessageToSend.text;
        channel.PushEvent("new_msg",new PayloadReq(msg));
    }

    void OnGUI(){
        ChatDisplay.text = chatDisplayString;
    }

}
