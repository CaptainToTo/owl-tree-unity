using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OwlTree;
using UnityEngine.Events;

namespace OwlTree.Unity
{

/// <summary>
/// Attach to game objects that need to be synchronized across clients.
/// </summary>
public class NetworkGameObject : MonoBehaviour
{
    /// <summary>
    /// The id this game object is assigned. This will be the same across all clients.
    /// Use to uniquely identify game objects.
    /// </summary>
    public GameObjectId Id { get; internal set; } = GameObjectId.None;
    
    /// <summary>
    /// The prefab this game object originally came from.
    /// </summary>
    public PrefabId Prefab { get; internal set; } = PrefabId.None;

    /// <summary>
    /// The connection this game object is managed by.
    /// </summary>
    public UnityConnection Connection { get; internal set; } = null;

    /// <summary>
    /// Whether or not this game object is currently being managed by a connection.
    /// </summary>
    public bool IsActive => Id != GameObjectId.None && Connection != null;

    /// <summary>
    /// Invoked when this game object is synchronously instantiated, and is now managed
    /// by a connection.
    /// </summary>
    public UnityEvent<NetworkGameObject> OnSpawn;

    internal void InvokeOnSpawn()
    {
        var behaviours = GetComponents<NetworkBehaviour>();
        foreach (var b in behaviours)
            b.OnSpawn();
        OnSpawn.Invoke(this);
    }

    /// <summary>
    /// Invoked when this game object is synchronously destroyed.
    /// </summary>
    public UnityEvent<NetworkGameObject> OnDespawn;

    internal void InvokeOnDespawn()
    {
        var behaviours = GetComponents<NetworkBehaviour>();
        foreach (var b in behaviours)
            b.OnDespawn();
        OnDespawn.Invoke(this);
    }
}

}
