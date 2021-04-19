using System;
using System.Runtime.Serialization;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

class Message
{
    public string type;
    public string sdp;
    public int index;
    public string id;
}

class DataChannelSample : MonoBehaviour
{
    #pragma warning disable 0649
    [SerializeField] private Button callButton;
    [SerializeField] private Button sendButton;
    [SerializeField] private InputField textSend;
    [SerializeField] private InputField textReceive;
    #pragma warning restore 0649

    private RTCPeerConnection pc1, pc2;
    private RTCDataChannel dataChannel, remoteDataChannel;
    private Coroutine sdpCheck;
    private string msg;
    private DelegateOnIceConnectionChange pc1OnIceConnectionChange;
    private DelegateOnIceConnectionChange pc2OnIceConnectionChange;
    private DelegateOnIceCandidate pc1OnIceCandidate;
    private DelegateOnIceCandidate pc2OnIceCandidate;
    private DelegateOnMessage onDataChannelMessage;
    private DelegateOnOpen onDataChannelOpen;
    private DelegateOnClose onDataChannelClose;
    private DelegateOnDataChannel onDataChannel;
    private WebSocket websocket, websocket2;

    private RTCOfferOptions OfferOptions = new RTCOfferOptions
    {
        iceRestart = false,
        offerToReceiveAudio = true,
        offerToReceiveVideo = false
    };

    private RTCAnswerOptions AnswerOptions = new RTCAnswerOptions
    {
        iceRestart = false,
    };

    private void Awake()
    {
        WebRTC.Initialize();
    }

    private void OnDestroy()
    {
        WebRTC.Dispose();
    }

    private async void Start()
    {
        websocket = new WebSocket("ws://localhost:8000/socket");

        Debug.Log("GetSelectedSdpSemantics");
        var configuration = GetSelectedSdpSemantics();

        pc2 = new RTCPeerConnection(ref configuration);
        Debug.Log("Created remote peer connection object pc2");

        pc2.OnIceCandidate = pc2OnIceCandidate;
        pc2.OnIceConnectionChange = pc2OnIceConnectionChange;
        pc2.OnDataChannel = onDataChannel;

        RTCDataChannelInit conf = new RTCDataChannelInit();
        dataChannel = pc2.CreateDataChannel("data", conf);
        dataChannel.OnOpen = onDataChannelOpen;

        //dataChannel.Send("3TEST");

        pc2OnIceConnectionChange = state => { OnIceConnectionChange(pc2, state); };
        pc2OnIceCandidate = candidate => { OnIceCandidate(pc2, candidate); };

        textReceive.text += "0TEST";

        onDataChannel = channel =>
        {
            Debug.Log("Data Channel works!");
            textReceive.text += "2TEST";
            dataChannel = channel;
            dataChannel.OnMessage = onDataChannelMessage;
        };
        onDataChannelMessage = bytes => 
        {
            textReceive.text = System.Text.Encoding.UTF8.GetString(bytes);

   //         var epoint = new Uint8Array(event.data, 92);

		 //   if (epoint.byteLength > 93) {

			//var dpoint = MessagePack.decode(epoint);
   //         console.log("Decoded pointarray:", dpoint);

			//var pointbuff = new Int8Array(dpoint);
   //         colate(pointbuff);
        };
        onDataChannelOpen = () => { textReceive.text += "1TEST"; };
        onDataChannelClose = () => { sendButton.interactable = false; };

        websocket.OnOpen += () =>
        {
            Debug.Log("Connection open!");
        };

        websocket.OnError += (e) =>
        {
            Debug.Log("Error! " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("Connection closed!");
        };

        websocket.OnMessage += (bytes) =>
        {
            Debug.Log("Message: " + System.Text.Encoding.UTF8.GetString(bytes));

            Message message = JsonConvert.DeserializeObject<Message>(System.Text.Encoding.UTF8.GetString(bytes));
            Debug.Log("New message: " + message.type);
            
            switch(message.type)
            {
                case "offer":
                
                    var remoteDesc = new RTCSessionDescription();
                    remoteDesc.type = 0;
                    remoteDesc.sdp = message.sdp;
                    pc2.SetRemoteDescription(ref remoteDesc);

                    var answer = pc2.CreateAnswer(ref AnswerOptions);
                    Debug.Log("Answer: " + answer.Desc.sdp);
                    Debug.Log("Answer Desc: " + answer.Desc.type);

                    var localDesc = new RTCSessionDescription();
                    localDesc.type = answer.Desc.type;
                    localDesc.sdp = message.sdp;
                    pc2.SetLocalDescription(ref localDesc);

                    Message newMessage = new Message();

                    newMessage.type = "answer";
                    newMessage.sdp = answer.Desc.sdp;

                    string output = JsonConvert.SerializeObject(newMessage);

                    websocket.SendText(output);

                    break;
                case "candidate":
                    RTCIceCandidateInit candidateMessage = new RTCIceCandidateInit();
                    candidateMessage.candidate = message.sdp;
                    candidateMessage.sdpMLineIndex = 0;
                    candidateMessage.sdpMid = "";

                    RTCIceCandidate candidate = new RTCIceCandidate(candidateMessage);

                    pc2.AddIceCandidate(candidate);
                    break;
                default:
                    Debug.Log("P2: We got something from the signaling server but we don't know what it is!");
                    Debug.Log("Take a look for yourself: " + message.type);
                    Debug.Log("Take a look for yourself: " + message.sdp);
                    break;
            }
        };

        await websocket.Connect();
    }

    RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new RTCIceServer[]
        {
            new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } }
        };

        return config;
    }
    void OnIceConnectionChange(RTCPeerConnection pc, RTCIceConnectionState state)
    {
        switch (state)
        {
            case RTCIceConnectionState.New:
                Debug.Log($"IceConnectionState: New");
                break;
            case RTCIceConnectionState.Checking:
                Debug.Log($"IceConnectionState: Checking");
                break;
            case RTCIceConnectionState.Closed:
                Debug.Log($"IceConnectionState: Closed");
                break;
            case RTCIceConnectionState.Completed:
                Debug.Log($"IceConnectionState: Completed");
                break;
            case RTCIceConnectionState.Connected:
                Debug.Log($"IceConnectionState: Connected");
                break;
            case RTCIceConnectionState.Disconnected:
                Debug.Log($"IceConnectionState: Disconnected");
                break;
            case RTCIceConnectionState.Failed:
                Debug.Log($"IceConnectionState: Failed");
                break;
            case RTCIceConnectionState.Max:
                Debug.Log($"IceConnectionState: Max");
                break;
            default:
                break;
        }
    }

    void Pc2OnIceConnectionChange(RTCIceConnectionState state)
    {
        OnIceConnectionChange(pc2, state);
    }

    void Pc2OnIceCandidate(RTCIceCandidate candidate)
    {
        OnIceCandidate(pc2, candidate);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pc"></param>
    /// <param name="streamEvent"></param>
    void OnIceCandidate(RTCPeerConnection pc, RTCIceCandidate candidate)
    {
        var data = candidate.Candidate;
        Message iceCanOut = new Message();
        iceCanOut.type = "candidate";
        iceCanOut.sdp = data.ToString();
        iceCanOut.index = 0;
        iceCanOut.id = "";

        string output = JsonConvert.SerializeObject(iceCanOut);

        websocket.SendText(output);
    }

    void Update()
    {
    #if !UNITY_WEBGL || UNITY_EDITOR
            websocket.DispatchMessageQueue();
    #endif
    }
}
