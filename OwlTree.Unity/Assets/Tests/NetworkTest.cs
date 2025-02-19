using UnityEngine;
using OwlTree;
using OwlTree.Unity;

public class NetworkTest : NetworkBehaviour
{
    
}

public class TestNetcode : NetworkObject
{
    [Rpc]
    public virtual void MyRpc(string val, [CallerId] ClientId caller = default)
    {
        Debug.Log($"Message from {caller}: {val}");
    }
}
