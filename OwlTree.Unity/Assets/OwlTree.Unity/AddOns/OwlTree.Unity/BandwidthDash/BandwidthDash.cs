using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OwlTree.Unity
{
    /// <summary>
    /// Simple diagnostics tool to display a connection's measured bandwidth usage.
    /// </summary>
    public class BandwidthDash : MonoBehaviour
    {
        [Tooltip("The connection this dash will display info about.")] 
        [SerializeField] private UnityConnection _connection;
        [Tooltip("The frequency at which the display will update at.")]
        [SerializeField] private float _updateFrequency = 0.5f;
        [Space]
        [SerializeField] private TextMeshProUGUI _clientText;
        [SerializeField] private TextMeshProUGUI _recvText;
        [SerializeField] private TextMeshProUGUI _sendText;
        [SerializeField] private TextMeshProUGUI _pingText;

        private float _lastUpdate;

        void Awake()
        {
            _connection.OnReady.AddListener((id) => _clientText.text = id.ToString() + " Bandwidth");
        }

        void Update()
        {
            if (Time.time - _lastUpdate > _updateFrequency && _connection?.Bandwidth != null)
            {
                var b = _connection.Bandwidth;
                _recvText.text = $"Recv: {b.IncomingKbPerSecond():F2} KB/s";
                _sendText.text = $"Send: {b.OutgoingKbPerSecond():F2} KB/s";
                if (_connection.LocalId != ClientId.None)
                    _connection.Ping(ClientId.None).OnResolved += OnPingResolved;
                else
                    _pingText.text = "Ping 0ms";
                _lastUpdate = Time.time;
            }
        }

        private int[] _prevPings = new int[16];
        private int _curPing = 0;

        private void OnPingResolved(PingRequest ping)
        {
            _prevPings[_curPing % _prevPings.Length] = ping.Ping;
            int avg = 0;
            foreach (var p in _prevPings)
                avg += p;
            avg /= _prevPings.Length;
            _pingText.text = $"Ping: {avg}ms";
            _curPing++;
        }
    }
}
