using Unity.Services.Relay.Models;

namespace Unity.Services.Multiplayer
{
    /// <summary>
    /// Represents Relay options.
    /// </summary>
    public class RelayNetworkOptions
    {
        /// <summary>
        /// The selected <see cref="RelayProtocol"/>
        /// to be used for relay communication.
        /// </summary>
        public RelayProtocol Protocol { get; }

        /// <summary>
        /// Force a specific Relay <a
        /// href="https://docs.unity.com/ugs/en-us/manual/relay/manual/locations-and-regions">region</a>
        /// to be used and skip auto-selection from <a
        /// href="https://docs.unity.com/ugs/en-us/manual/relay/manual/qos">QoS</a>.
        /// </summary>
        public string Region { get; }

        /// <summary>
        /// Save the relay <a
        /// href="https://docs.unity.com/ugs/en-us/manual/relay/manual/locations-and-regions">region</a>
        /// to session properties and reuse it in case of host migration.
        /// </summary>
        public bool PreserveRegion { get; }

        /// <summary>
        /// Gets the default <see cref="RelayNetworkOptions"/>
        /// instance with <see cref="RelayProtocol.Default"/>.
        /// </summary>
        public static RelayNetworkOptions Default => new(RelayProtocol.Default);

        /// <summary>
        /// Creates a new <see cref="RelayNetworkOptions"/>
        /// instance with the given settings.
        /// </summary>
        /// <param name="protocol">
        /// The <see cref="RelayProtocol"/> to use for relay communication.
        /// If the protocol is not supported, the constructor uses
        /// <see cref="RelayProtocol.Default"/> and logs a warning.
        /// </param>
        /// <remarks>
        /// If the protocol is not supported, the constructor uses
        /// <see cref="RelayProtocol.Default"/> and logs a warning.
        /// </remarks>
        public RelayNetworkOptions(RelayProtocol protocol) : this(protocol, null, false)
        {
        }

        /// <summary>
        /// Creates a new <see cref="RelayNetworkOptions"/>
        /// instance with the given settings.
        /// </summary>
        /// <param name="protocol">
        /// The <see cref="RelayProtocol"/> to use for relay communication.
        /// If the protocol is not supported, the constructor uses
        /// <see cref="RelayProtocol.Default"/> and logs a warning.
        /// </param>
        /// <param name="region">
        /// The relay <a
        /// href="https://docs.unity.com/ugs/en-us/manual/relay/manual/locations-and-regions">region</a>
        /// to use. This skips auto-selection from <a
        /// href="https://docs.unity.com/ugs/en-us/manual/relay/manual/qos">QoS</a>.
        /// </param>
        /// <remarks>
        /// To get available regions, call <see
        /// cref="Unity.Services.Relay.IRelayService.ListRegionsAsync"/>.
        /// <br/>
        /// To find the lowest latency region, use <see
        /// cref="Unity.Services.Qos.IQosService.GetSortedRelayQosResultsAsync"/>.
        /// <br/>
        /// If the protocol is not supported, the constructor uses
        /// <see cref="RelayProtocol.Default"/> and logs a warning.
        /// </remarks>
        public RelayNetworkOptions(RelayProtocol protocol, string region) : this(protocol, region, false)
        {
        }

        /// <summary>
        /// Creates a new <see cref="RelayNetworkOptions"/>
        /// instance with the given settings.
        /// </summary>
        /// <param name="protocol">
        /// The <see cref="RelayProtocol"/> to use for relay communication.
        /// If the protocol is not supported, the constructor uses
        /// <see cref="RelayProtocol.Default"/> and logs a warning.
        /// </param>
        /// <param name="region">
        /// The relay <a
        /// href="https://docs.unity.com/ugs/en-us/manual/relay/manual/locations-and-regions">region</a>
        /// to use. This skips auto-selection from <a
        /// href="https://docs.unity.com/ugs/en-us/manual/relay/manual/qos">QoS</a>.
        /// </param>
        /// <param name="preserveRegion">
        /// If <c>true</c>, saves the relay region to session
        /// properties and reuses it for host migration.
        /// </param>
        /// <remarks>
        /// To get available regions, call <see
        /// cref="Unity.Services.Relay.IRelayService.ListRegionsAsync"/>.
        /// <br/>
        /// To find the lowest latency region, use <see
        /// cref="Unity.Services.Qos.IQosService.GetSortedRelayQosResultsAsync"/>.
        /// <br/>
        /// If the protocol is not supported, the constructor uses
        /// <see cref="RelayProtocol.Default"/> and logs a warning.
        /// </remarks>
        public RelayNetworkOptions(RelayProtocol protocol, string region, bool preserveRegion)
        {
            Protocol = GetValidProtocol(protocol);
            Region = region;
            PreserveRegion = preserveRegion;

            return;

            static RelayProtocol GetValidProtocol(RelayProtocol protocol)
            {
                if (AllocationUtils.IsValidProtocol(protocol))
                {
                    return protocol;
                }

                Logger.LogWarning($"Invalid protocol \"{protocol:G}\" detected. Using default protocol \"{RelayProtocol.Default:G}\" instead.");

                return RelayProtocol.Default;
            }
        }
    }
}
