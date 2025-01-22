using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OwlTree;

namespace OwlTree.Unity
{

/// <summary>
/// Only provides getters for the NetworkGameObject component
/// attached to this GameObject, and Connection managing it.
/// This component can exist outside of a network environment.
/// </summary>
public abstract class NetworkBehaviour : MonoBehaviour
{
    /// <summary>
    /// The NetworkGameObject component attached to this GameObject.
    /// If there is not NetworkGameObject component, returns null.
    /// </summary>
    public NetworkGameObject NetObject { get {
        if (_netObj == null)
            _netObj = GetComponent<NetworkGameObject>();
        return _netObj;
    }}
    private NetworkGameObject _netObj = null;

    /// <summary>
    /// The UnityConnection that's managing this object. If there is no
    /// connection, returns null.
    /// </summary>
    public UnityConnection Connection => NetObject?.Connection;

    /// <summary>
    /// Invoked when this object is instantiated synchronously by a connection.
    /// This can be treated as a stand-in for Start().
    /// </summary>
    public virtual void OnSpawn() { }

    /// <summary>
    /// Invoked when this object is destroyed synchronously by a connection.
    /// This can be treated as a stand-in for Destroy().
    /// </summary>
    public virtual void OnDespawn() { }
}

}
