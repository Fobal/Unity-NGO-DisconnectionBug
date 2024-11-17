using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Unity.Netcode.Transports.UTP;
using Unity.Collections;
using static CustomNetMessages;
using System.Linq;

public class ClientData
{
    public ulong connectionId;
    // TODO: Add more client data here

    public ClientData(ulong connectionId)
    {
        this.connectionId = connectionId;
    }
}

[System.Serializable]
public class ServerConfigData
{
    public string ssl_certificate_path;
    public string ssl_certificate_key_path;
}

public class ServerManager : MonoBehaviour
{
    [SerializeField] private ushort port;
    private UnityTransport _transport;
    private NetworkManager _networkManager;
    private readonly Dictionary<ulong, ClientData> netcode_client_connections = new();  //List of clients | connectionId -> ClientData   
    private const int k_MaxConnectPayload = 1024;
    private const int TARGET_FRAME_RATE = 60;
    private const int MSG_SIZE_MAX = 1024 * 1024 * 10; // 10MB
    private static ServerManager _instance;

    private void Awake()
    {
        _instance = this;

        _transport = Object.FindFirstObjectByType<UnityTransport>();
        _networkManager = Object.FindFirstObjectByType<NetworkManager>();

        Application.runInBackground = true;
        Application.targetFrameRate = TARGET_FRAME_RATE;

        _networkManager.ConnectionApprovalCallback += ApprovalCheck;
        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        _networkManager.ConnectionApprovalCallback -= ApprovalCheck;
        _networkManager.OnClientConnectedCallback -= OnClientConnected;
        _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        _instance = null;
    }

    private void Start()
    {
        _transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
        SetTransportSSLCertificate();
        _networkManager.StartServer();
        Debug.Log($"Game server listening on port {port}");

        StartCoroutine(BroadcastMessageToClients());
    }

    private void Update()
    {
        if (Application.targetFrameRate != TARGET_FRAME_RATE)
        {
            Application.targetFrameRate = TARGET_FRAME_RATE;
        }
    }

    private IEnumerator BroadcastMessageToClients()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.2f);

            for (int i = 0; i < netcode_client_connections.Count; i++)
            {
                ulong clientId = netcode_client_connections.Keys.ElementAt(i);

                MsgTest msg_data = new MsgTest();
                msg_data.bool_value = true;
                msg_data.int_value = 1;
                msg_data.ulong_value = clientId;

                using (FastBufferWriter writer = new FastBufferWriter(2048, Allocator.Temp, MSG_SIZE_MAX))
                {
                    writer.WriteValueSafe(msg_data);
                    SendMsgToClient(clientId, "TestMsg", writer, NetworkDelivery.ReliableFragmentedSequenced);
                }
            }
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {

        Debug.Log("--ApprovalCheck Start--");

        var connectionData = request.Payload;

        if (connectionData.Length > k_MaxConnectPayload)
        {
            // If connectionData too high, deny immediately to avoid wasting time on the server. This is intended as
            // a bit of light protection against DOS attacks that rely on sending silly big buffers of garbage.
            response.Approved = false;
            return;
        }

        var clientId = request.ClientNetworkId;

        response.Approved = true;

        response.Pending = false;

        Debug.Log("--ApprovalCheck End--");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        netcode_client_connections[clientId] = new ClientData(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
        netcode_client_connections.Remove(clientId);
    }

    private void SetTransportSSLCertificate()
    {
        //Read the config file
        string path = System.IO.Path.Combine(Application.dataPath, "../../gameServerConfig/config.json");
        path = System.IO.Path.GetFullPath(path);
        if (System.IO.File.Exists(path))
        {
            string json = System.IO.File.ReadAllText(path);
            ServerConfigData config = JsonUtility.FromJson<ServerConfigData>(json);

            if (config != null && !string.IsNullOrEmpty(config.ssl_certificate_path) && !string.IsNullOrEmpty(config.ssl_certificate_key_path))
            {
                if (System.IO.File.Exists(config.ssl_certificate_path) && System.IO.File.Exists(config.ssl_certificate_key_path))
                {
                    // read the certificate and key
                    byte[] cert = System.IO.File.ReadAllBytes(config.ssl_certificate_path);
                    byte[] key = System.IO.File.ReadAllBytes(config.ssl_certificate_key_path);

                    // to string
                    string cert_str = System.Text.Encoding.UTF8.GetString(cert);
                    string key_str = System.Text.Encoding.UTF8.GetString(key);

                    //Debug.Log("SSL Certificate: " + cert_str);
                    //Debug.Log("SSL Key: " + key_str);

                    if (!string.IsNullOrEmpty(cert_str) && !string.IsNullOrEmpty(key_str))
                    {
                        _transport.SetServerSecrets(cert_str, key_str);
                        Debug.Log("SSL Certificate set");

                        return;
                    }
                    else
                    {
                        Debug.LogError("SSL certificate is empty! Transport will use unsecure connection, which may not work on WebGL");
                        return;
                    }
                }
            }

            Debug.LogError("No SSL certificate found! Transport will use unsecure connection, which may not work on WebGL");
        }

        Debug.LogError("No config file found! Transport will use unsecure connection, which may not work on WebGL");
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

    private void SendMsgToClient(ulong clientId, string name, FastBufferWriter writer, NetworkDelivery delivery = NetworkDelivery.ReliableSequenced)
    {
        _networkManager.CustomMessagingManager.SendNamedMessage(name, clientId, writer, delivery);
        Debug.Log($"Sent message to client {clientId}. MsgName: {name}, MsgLength: {writer.Length}");
    }

    #endregion

    public static ServerManager Get()
    {
        return _instance;
    }
}
