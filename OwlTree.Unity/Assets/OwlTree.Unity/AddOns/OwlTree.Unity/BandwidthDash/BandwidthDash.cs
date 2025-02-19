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

        private float _lastUpdate;

        void Awake()
        {
            _connection.OnReady.AddListener((id) => _clientText.text = id.ToString() + " Bandwidth");
        }

        void Update()
        {
            if (Time.time - _lastUpdate > _updateFrequency)
            {
                var b = _connection.Bandwidth;
                _recvText.text = $"Recv: {b.IncomingKbPerSecond():F2} KB/s";
                _sendText.text = $"Send: {b.OutgoingKbPerSecond():F2} KB/s";
                _lastUpdate = Time.time;
            }
        }
    }
}
