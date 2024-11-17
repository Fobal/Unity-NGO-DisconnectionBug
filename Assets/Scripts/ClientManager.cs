using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using static CustomNetMessages;

public class ClientManager : MonoBehaviour
{
    [SerializeField] private string serverCommonName;
    [SerializeField] private string ip;
    [SerializeField] private ushort port;
    private NetworkManager _networkManager;
    private UnityTransport _transport;
    private static ClientManager _instance;
    private void Awake()
    {
        _instance = this;

        _networkManager = Object.FindFirstObjectByType<NetworkManager>();
        _transport = Object.FindFirstObjectByType<UnityTransport>();

        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        _networkManager.OnClientConnectedCallback -= OnClientConnected;
        _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        UnregisterNetMsg("TestMsg");
        _instance = null;
    }

    void Start()
    {
        _transport.SetConnectionData(ip, port);
        _transport.SetClientSecrets(serverCommonName);
        _networkManager.StartClient();

        RegisterNetMsg("TestMsg", OnTestMsg);
    }

    void Update()
    {

    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
    }

    #region CustomMessaging

    public void UnregisterNetMsg(string name)
    {
        _networkManager.CustomMessagingManager?.UnregisterNamedMessageHandler(name);
    }

    private void RegisterNetMsg(string name, System.Action<ulong, FastBufferReader> callback)
    {
        _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(name, (ulong client_id, FastBufferReader reader) =>
        {
            callback(client_id, reader);
        });
    }

    private void SendMsgToServer(string name, FastBufferWriter writer, NetworkDelivery delivery = NetworkDelivery.ReliableSequenced)
    {
        _networkManager.CustomMessagingManager.SendNamedMessage(name, NetworkManager.ServerClientId, writer, delivery);
    }

    // Messages

    private void OnTestMsg(ulong clientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out MsgTest msg);
        Debug.Log($"Received message from server. MsgLength: {reader.Length}, Msg: {msg}");
    }

    #endregion

    public static ClientManager Get()
    {
        return _instance;
    }
}
