using UnityEngine;
using System.Collections;

public class TestJson : MonoBehaviour {
	void Start () {
		Message<PayloadReq> msg = new Message<PayloadReq> ("topic", "event", new PayloadReq ("payload"), "ref");
		var jsonMessage = JsonUtility.ToJson (msg);
		Debug.Log (jsonMessage);

	}
	
}
