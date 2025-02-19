# OwlTree.Matchmaking.Unity

Provides a basic matchmaking service that can be used to collect necessary information to create connections.
This is separate from OwlTree.Matchmaking because it uses Unity's JsonUtility, instead of the System.Text.Json encoder.

The interface is identical. Use this to create a matchmaking client in Unity.

## In the client application:

Create a new `MatchmakingClient`, send a request, and await a response:

```cs
var requestClient = new MatchmakingClient("http://localhost:3000");

var response = await requestClient.MakeRequest(new MatchmakingRequest{
    appId = "MyOwlTreeApp",
    sessionId = "MyOwlTreeAppSession",
    serverType = ServerType.Relay,
    ClientRole = ClientRole.Host,
    maxClients = 10,
    migratable = false,
    owlTreeVersion = 1,
    appVersion = 1
});

if (response.RequestFailed)
    return;

var connection = new Connection(new Connection.Args{
    role = NetRole.Host,
    serverAddr = response.serverAddr,
    tcpPort = response.tcpPort,
    udpPort = response.udpPort,
    appId = response.appId,
    sessionId = response.sessionId
});
```