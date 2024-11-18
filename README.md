The server sends a message every 0.2s to all clients and when someone disconnects, the netcode freezes. Error message that I get:
```
Error sending message: Unable to queue packet in the transport. Likely caused by send queue size ('Max Send Queue Size') being too small.
CompleteSend failed with the following error code: -5
```
The rest of the functions work as intended, cpu and ram are at normal levels, it's just the netcode that gets frozen.

How to replicate:
Setup the ip,port,serverCommonName on ClientManager and the port,certificates on the ServerManager. Connect at least 2 clients. Then while the server sends the custom messages, just disconnect one client (by losing internet access).
